﻿using System;
using System.Diagnostics;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using Windows.UI.Popups;
using Windows.UI.Notifications;
using System.Threading.Tasks;

namespace WinIRC.Net
{
    public class IrcWebSocket : Irc
    {
        private MessageWebSocket messageWebSocket;

        public override async void Connect()
        {
            // Make a local copy to avoid races with Closed events.
            MessageWebSocket webSocket = messageWebSocket;
            try
            {
                var protectionLevel = server.ssl ? "wss" : "ws";
                var socketUri = new Uri(protectionLevel + "://" + server.hostname + ":" + server.port + "/", UriKind.Absolute);
                Debug.WriteLine("Attempting to connect...");

                if (webSocket == null)
                {
                    Uri server = socketUri;

                    webSocket = new MessageWebSocket();

                    if (Config.GetBoolean(Config.IgnoreSSL))
                        webSocket.ServerCustomValidationRequested += WebSocket_ServerCustomValidationRequested;

                    // MessageWebSocket supports both utf8 and binary messages.
                    // When utf8 is specified as the messageType, then the developer
                    // promises to only send utf8-encoded data.
                    webSocket.Control.MessageType = SocketMessageType.Utf8;
                    // Set up callbacks
                    webSocket.MessageReceived += MessageReceived;
                    webSocket.Closed += Closed;

                    await webSocket.ConnectAsync(server);
                    messageWebSocket = webSocket; // Only store it after successfully connecting.
                }

                Debug.WriteLine("Connected!");

                writer = new DataWriter(webSocket.OutputStream);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace);
                var messageDialog = new MessageDialog("Could not connect to the server.");
                await messageDialog.ShowAsync();
                HandleDisconnect(this);
            }

        }

        private void WebSocket_ServerCustomValidationRequested(MessageWebSocket sender, WebSocketServerCustomValidationRequestedEventArgs args)
        {
            // allowed
        }

        private void Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            var autoReconnect = Config.GetBoolean(Config.AutoReconnect);

            var msg = autoReconnect
                ? "Attempting to reconnect..."
                : "Please try again later.";

            var error = Irc.CreateBasicToast("Websocket closed", msg);

            ToastNotificationManager.CreateToastNotifier().Show(error);

            DisconnectAsync(attemptReconnect: autoReconnect);
        }

        public override async void DisconnectAsync(string msg = "Powered by WinIRC", bool attemptReconnect = false)
        {
            WriteLine("QUIT :" + msg);
            if (attemptReconnect)
            {
                IsReconnecting = true;
                if (ConnCheck.HasInternetAccess)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, async () =>
                    {
                        await Task.Delay(100);
                        Connect();
                    });
                }
            }
            else
            {
                IsConnected = false;
                HandleDisconnect(this);
                messageWebSocket.Dispose();
            }
        }

        private async void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            // container for the received Data
            // in this case a string
            string receivedData = "";
            try
            {
                using (DataReader reader = args.GetDataReader())
                {
                    var count = reader.UnconsumedBufferLength;
                    // read the data as a string and store it in our container
                    if (count > 0)
                    {
                        receivedData = reader.ReadString(count);

                        if (receivedData == "SOCKET_CONNECTED_IRC")
                        {
                            AttemptAuth();
                            return;
                        }

                        if (IsAuthed)
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                HandleLine(receivedData);
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WebErrorStatus status = WebSocketError.GetStatus(e.GetBaseException().HResult);
                Debug.WriteLine("Error with recieving a message: " + status);
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
            }
        }
    }
}
