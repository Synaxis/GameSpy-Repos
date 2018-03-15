using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
    public delegate void ConnectionUpdate(GpcmClient client);

    public delegate void GpspConnectionClosed(GpspClient client);

    public delegate void GpcmConnectionClosed(GpcmClient client);
}
