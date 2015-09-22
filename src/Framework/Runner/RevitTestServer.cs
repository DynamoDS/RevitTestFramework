using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using Autodesk.RevitAddIns;
using Dynamo.NUnit.Tests;
using Microsoft.Practices.Prism;
using NDesk.Options;

namespace RTF.Framework
{
    public class RevitTestServer
    {
        private Socket serverSocket;
        private Socket handlerSocket;
        private int iPort;

        private static RevitTestServer instance;
        private static readonly Object mutex = new Object();
        private static MessageBuffer buffer;
        private static int receiveTimeout;

        /// <summary>
        /// A singleton instance
        /// </summary>
        public static RevitTestServer Instance
        {
            get
            {
                lock (mutex)
                {
                    return instance ?? (instance = new RevitTestServer());
                }
            }
        }

        /// <summary>
        /// Start the server at localhost
        /// </summary>
        /// <param name="timeout"></param>
        public void Start(int timeout)
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(ipAddress, 0));

            IPEndPoint endPoint = serverSocket.LocalEndPoint as IPEndPoint;
            iPort = endPoint.Port;

            serverSocket.Listen(1);
            receiveTimeout = timeout;
        }

        /// <summary>
        /// End the server
        /// </summary>
        public void End()
        {
            if (handlerSocket != null)
            {
                handlerSocket.Close();
                handlerSocket = null;
            }
            if (serverSocket != null)
            {
                serverSocket.Close();
                serverSocket = null;
            }
        }

        /// <summary>
        /// Close the working socket and also reset the message ID
        /// </summary>
        public void ResetWorkingSocket()
        {
            if (handlerSocket != null)
            {
                handlerSocket.Close();
                handlerSocket = null;
            }
            MessageResult.PrevMessageID = -1;
        }

        /// <summary>
        /// This will try to accept a connection from localhost and receive a packet.
        /// It will then buffer the packet and try to create a message.
        /// </summary>
        /// <returns></returns>
        public MessageResult GetNextMessageResult()
        {
            MessageResult result = new MessageResult();
            if (handlerSocket == null)
            {
                handlerSocket = AcceptLocalConnection();
                handlerSocket.ReceiveTimeout = receiveTimeout;
            }

            if (buffer != null)
            {
                var msg = buffer.GetMessage();
                if (msg != null)
                {
                    result.Message = msg;
                    return result;
                }
            }

            var bytes = new byte[1024];
            int size = 0;
            try
            {
                size = handlerSocket.Receive(bytes, 1024, SocketFlags.None);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    result.Status = MessageStatus.TimedOut;
                    return result;
                }
                else
                {
                    result.Status = MessageStatus.OtherError;
                    return result;
                }
            }
            if (size > 0)
            {
                if (buffer == null)
                {
                    buffer = new MessageBuffer(bytes.Take(size).ToArray());
                }
                else
                {
                    buffer.Add(bytes.Take(size).ToArray());
                }
                result.Message = buffer.GetMessage();
            }

            return result;
        }

        /// <summary>
        /// To accept a connection from localhost only
        /// </summary>
        /// <returns></returns>
        private Socket AcceptLocalConnection()
        {
            Socket handlerSocket = null;
            while (true)
            {
                handlerSocket = serverSocket.Accept();
                var endPoint = handlerSocket.RemoteEndPoint as IPEndPoint;
                if (endPoint != null && string.CompareOrdinal(endPoint.Address.ToString(), "127.0.0.1") == 0)
                {
                    break;
                }
                handlerSocket.Close();
            }
            return handlerSocket;
        }

        protected Socket ServerSocket
        {
            get
            {
                return serverSocket;
            }
        }

        /// <summary>
        /// The port number for the server to be connected to
        /// </summary>
        public int Port
        {
            get
            {
                return iPort;
            }
        }
    }
}
