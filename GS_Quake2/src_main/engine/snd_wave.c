/*
 * WAV codec support. Adapted from ioquake3 with changes.
 *
 * Copyright (C) 1999-2005 Id Software, Inc.
 * Copyright (C) 2005 Stuart Dalton <badcdev@gmail.com>
 * Copyright (C) 2010 O.Sezer <sezero@users.sourceforge.net>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *
 * See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to the Free Software Foundation, Inc.,
 * 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
 *
 */

#include "client.h"
#include "snd_loc.h"

#define USE_CODEC_WAVE

#if defined(USE_CODEC_WAVE)
#include "snd_codec.h"
#include "snd_codeci.h"
#include "snd_wave.h"

/*
=================
FGetLittleLong
=================
*/
static int FGetLittleLong (FILE *f)
{
	int		v;

	fread(&v, 1, sizeof(v), f);

	return LittleLong(v);
}

/*
=================
FGetLittleShort
=================
*/
static short FGetLittleShort(FILE *f)
{
	short	v;

	fread(&v, 1, sizeof(v), f);

	return LittleShort(v);
}

/*
=================
WAV_ReadChunkInfo
=================
*/
static int WAV_ReadChunkInfo(FILE *f, char *name)
{
	int len, r;

	name[4] = 0;

	r = fread(name, 1, 4, f);
	if (r != 4)
		return -1;

	len = FGetLittleLong(f);
	if (len < 0)
	{
		Com_Printf("WAV: Negative chunk length\n");
		return -1;
	}

	return len;
}

/*
=================
WAV_FindRIFFChunk
Returns the length of the data in the chunk, or -1 if not found
=================
*/
static int WAV_FindRIFFChunk(FILE *f, const char *chunk)
{
	char	name[5];
	int		len;

	while ((len = WAV_ReadChunkInfo(f, name)) >= 0)
	{
		// If this is the right chunk, return
		if (!strncmp(name, chunk, 4))
			return len;
		len = ((len + 1) & ~1);	// pad by 2 .

		// Not the right chunk - skip it
		fseek(f, len, SEEK_CUR);
	}

	return -1;
}

/*
=================
S_ByteSwapRawSamples
=================
*/
static void S_ByteSwapRawSamples(int samples, int width, int s_channels, const byte *data)
{
	int		i;
	
	if (width != 2) {
		return;
	}

	if (LittleShort(256) == 256) {
		return;
	}

	if (s_channels == 2) {
		samples <<= 1;
	}

	for (i = 0; i < samples; i++) {
		((short *)data)[i] = LittleShort(((short *)data)[i]);
	}
}

/*
=================
WAV_ReadRIFFHeader
=================
*/
static qboolean WAV_ReadRIFFHeader(const char *name, FILE *file, snd_info_t *info)
{
	char dump[16];
	int wav_format;
	int bits;
	int fmtlen = 0;

	if (fread(dump, 1, 12, file) < 12 ||
	    strncmp(dump, "RIFF", 4) != 0 ||
	    strncmp(&dump[8], "WAVE", 4) != 0)
	{
		Com_Printf("%s is missing RIFF/WAVE chunks\n", name);
		return false;
	}

	// Scan for the format chunk
	if ((fmtlen = WAV_FindRIFFChunk(file, "fmt ")) < 0)
	{
		Com_Printf("%s is missing fmt chunk\n", name);
		return false;
	}

	// Save the parameters
	wav_format = FGetLittleShort(file);
	if (wav_format != WAV_FORMAT_PCM)
	{
		Com_Printf("%s is not Microsoft PCM format\n", name);
		return false;
	}

	info->channels = FGetLittleShort(file);
	info->rate = FGetLittleLong(file);
	FGetLittleLong(file);
	FGetLittleShort(file);
	bits = FGetLittleShort(file);

	if (bits != 8 && bits != 16)
	{
		Com_Printf("%s is not 8 or 16 bit\n", name);
		return false;
	}

	info->width = bits / 8;
	info->dataofs = 0;

	// Skip the rest of the format chunk if required
	if (fmtlen > 16)
	{
		fmtlen -= 16;
		fseek(file, fmtlen, SEEK_CUR);
	}

	info->loopstart = -1;

	// Scan for the data chunk
	if ((info->size = WAV_FindRIFFChunk(file, "data")) < 0)
	{
		Com_Printf("%s is missing data chunk\n", name);
		return false;
	}

	info->samples = (info->size / info->width) / info->channels;
	if (info->samples == 0)
	{
		Com_Printf("%s has zero samples\n", name);
		return false;
	}

	return true;
}

/*
=================
S_WAV_CodecLoad
=================
*/
void *S_WAV_CodecLoad(char *filename, snd_info_t *info)
{
	FILE *file;
	void *buffer;
	
	// Try to open the file
	FS_FOpenFile(filename, &file);
	if (!file)
	{
		Com_Printf("Can't read sound file %s\n", filename);
		return NULL;
	}
	
	// Read the RIFF header
	if (!WAV_ReadRIFFHeader(filename, file, info))
	{
		FS_FCloseFile(file);
		Com_Printf("Can't understand wav file %s\n", filename);
		return NULL;
	}
	
	// Allocate some memory
	buffer = Z_Malloc(info->size);
	if (!buffer)
	{
		FS_FCloseFile(file);
		Com_Printf("Out of memory reading %s\n", filename);
		return NULL;
	}
	
	// Read, byteswap
	FS_Read(buffer, info->size, file);
	S_ByteSwapRawSamples(info->samples, info->width, info->channels, (byte *)buffer);
	
	// Close and return
	FS_FCloseFile(file);
	return buffer;
}

/*
=================
S_WAV_CodecOpenStream
=================
*/
snd_stream_t *S_WAV_CodecOpenStream(char *filename)
{
	snd_stream_t *stream;
	long start;

	stream = S_CodecUtilOpen(filename, &wav_codec);
	if (!stream)
		return NULL;

	start = stream->fh.start;

	// Read the RIFF header
	/* The file reads are sequential, therefore no need
	 * for the FS_*() functions: We will manipulate the
	 * file by ourselves from now on. */
	if (!WAV_ReadRIFFHeader(filename, stream->fh.file, &stream->info))
	{
		S_CodecUtilClose(&stream);
		return NULL;
	}

	stream->fh.start = ftell(stream->fh.file); // reset to data position
	if (stream->fh.start - start + stream->info.size > stream->fh.length)
	{
		Com_Printf("%s data size mismatch\n", filename);
		S_CodecUtilClose(&stream);
		return NULL;
	}

	return stream;
}

/*
=================
S_WAV_CodecReadStream
=================
*/
int S_WAV_CodecReadStream(snd_stream_t *stream, int bytes, void *buffer)
{
	int remaining = stream->info.size - stream->fh.pos;
	int i, samples;

	if (remaining <= 0)
		return 0;
	if (bytes > remaining)
		bytes = remaining;
	stream->fh.pos += bytes;
	fread(buffer, 1, bytes, stream->fh.file);
	if (stream->info.width == 2)
	{
		samples = bytes / 2;
		for (i = 0; i < samples; i++)
			((short *)buffer)[i] = LittleShort( ((short *)buffer)[i] );
	}
	return bytes;
}

static void S_WAV_CodecCloseStream (snd_stream_t *stream)
{
	S_CodecUtilClose(&stream);
}

static int S_WAV_CodecRewindStream (snd_stream_t *stream)
{
	FS_rewind(&stream->fh);
	return 0;
}

static qboolean S_WAV_CodecInitialize (void)
{
	return true;
}

static void S_WAV_CodecShutdown (void)
{
}

snd_codec_t wav_codec =
{
	CODECTYPE_WAV,
	true,	// always available.
	".wav",
	S_WAV_CodecInitialize,
	S_WAV_CodecShutdown,
	S_WAV_CodecLoad,
	S_WAV_CodecOpenStream,
	S_WAV_CodecReadStream,
	S_WAV_CodecRewindStream,
	S_WAV_CodecCloseStream,
	NULL
};

#endif	/* USE_CODEC_WAVE */
