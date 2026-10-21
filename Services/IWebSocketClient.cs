using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FRCM
{
    public interface IWebSocketClient
    {
        event EventHandler<string> MessageReceived;
        event EventHandler<byte[]>? BinaryMessageReceived;
        Task ConnectAsync(Uri uri, string token);
        Task DisconnectAsync();
        bool IsConnected { get; }
        Task SendAsync(string message);
        Task<bool> RefreshTokenAsync();

    }
}
