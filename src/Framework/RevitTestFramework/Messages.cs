using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace RTF.Framework
{
    /// <summary>
    /// An utility class for deserialising messages
    /// </summary>
    class MessageBinder : SerializationBinder
	{
	    public override System.Type BindToType(string assemblyName, string typeName)
	    {
	        var result = AppDomain.CurrentDomain.GetAssemblies()
	            .Where(a => !a.IsDynamic)
                .Where(a => string.CompareOrdinal(a.FullName, assemblyName) == 0)
	            .SelectMany(a => a.GetTypes())
	            .FirstOrDefault(t => string.CompareOrdinal(t.FullName, typeName) == 0);
	
	        return result;
	    }
	}

    /// <summary>
    /// The base class for all message classes. It has an ID which will
    /// increase automatically whenever a new message is created
    /// </summary>
    [Serializable]
    public class Message : ISerializable
    {
        static long currentID = 0;

        public Message()
        {
            MessageID = Interlocked.Increment(ref currentID);
        }

        public Message(SerializationInfo info, StreamingContext context)
        {
            MessageID = (long)info.GetValue("MessageID", MessageID.GetType());
        }

        public long MessageID
        {
            get;
            private set;
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("MessageID", MessageID);
        }

        public static byte[] ToBytes(Message message)
        {
            IFormatter formatter = new BinaryFormatter();
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, message);
                return stream.ToArray();
            }
        }

        public static Message FromBytes(byte[] bytes)
        {
            try
            {
                IFormatter formatter = new BinaryFormatter();
                formatter.Binder = new MessageBinder();
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    return (Message)formatter.Deserialize(stream);
                }
            }
            catch(Exception e)
            {
                string msg = e.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// This is the class for data messages which contain information for
    /// running test cases
    /// </summary>
    [Serializable]
    public class DataMessage : Message
    {
        public DataMessage(string testCaseName, string fixtureName)
        {
            TestCaseName = testCaseName;
            FixtureName = fixtureName;
        }

        public DataMessage(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            TestCaseName = (string)info.GetValue("TestCaseName", typeof(string));
            FixtureName = (string)info.GetValue("FixtureName", typeof(string));
        }

        public string TestCaseName
        {
            get;
            set;
        }

        public string FixtureName
        {
            get;
            set;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("TestCaseName", TestCaseName);
            info.AddValue("FixtureName", FixtureName);
        }
    }

    /// <summary>
    /// This is the class for control messages which contain information to
    /// identify the status of the client
    /// </summary>
    [Serializable]
    public class ControlMessage : Message
    {
        public ControlMessage(ControlType type)
        {
            Type = type;
        }

        public ControlMessage(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Type = (ControlType)info.GetValue("ControlType", typeof(ControlType));
        }

        public ControlType Type
        {
            get;
            set;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ControlType", Type);
        }
    }

    /// <summary>
    /// This is the class for console log messages
    /// </summary>
    [Serializable]
    public class ConsoleOutMessage : Message
    {
        public ConsoleOutMessage(ConsoleMessageType type, string text)
        {
            Type = type;
            Text = text;
        }

        public ConsoleOutMessage(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Type = (ConsoleMessageType)info.GetValue("MessageType", typeof(ConsoleMessageType));
            Text = (string)info.GetValue("MessageText", typeof(string));
        }

        public ConsoleMessageType Type
        {
            get;
            set;
        }

        public string Text
        {
            get;
            set;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("MessageType", Type);
            info.AddValue("MessageText", Text);
        }
    }

    [Serializable]
    public enum ControlType
    {
        NotificationOfStart = 0,
        NotificationOfEnd = 1
    }

    [Serializable]
    public enum ConsoleMessageType
    {
        ConsoleOut = 0,
        ErrorOut = 1,
        DebugOut = 2,
    }

    /// <summary>
    /// This is an helper class to prepend a header before a given message
    /// </summary>
    public class MessageHelper
    {
        /// <summary>
        /// Prepend a 4 byte array which stores the length of the input array.
        /// NOTE that the length will not count in the 4 bytes to store the length itself
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] AddHeader(byte[] bytes)
        {
            int len = bytes.Length;
            byte[] header = BitConverter.GetBytes(len);
            var result = header.Concat(bytes);
            return result.ToArray();
        }
    }

    /// <summary>
    /// A buffer to buffer incoming bytes arrays and include function to 
    /// deserialise messages from the buffer
    /// </summary>
    public class MessageBuffer
    {
        List<byte[]> bytesList;

        public MessageBuffer(byte[] bytes)
        {
            bytesList = new List<byte[]>();
            bytesList.Add(bytes);
        }

        public void Add(byte[] bytes)
        {
            bytesList.Add(bytes);
        }

        /// <summary>
        /// This will check the buffer to say if it contains bytes for a complete message.
        /// If so, it will return a message converted from the bytes.
        /// It may be possible that one byte array contains bytes more than one message or
        /// several byte arrays together can generate one message. For these cases, one array
        /// may be cut or arrays may be joined to a byte array which can be converted to a
        /// message
        /// </summary>
        /// <returns></returns>
        public Message GetMessage()
        {
            // Return null if nothing is buffered
            int count = bytesList.Count;
            if (count == 0)
            {
                return null;
            }

            // Return null if there are buffered bytes, but they are not
            // enough for a complete message
            var msgBytes = bytesList.ElementAt(0);
            int msgSize = BitConverter.ToInt32(msgBytes, 0);
            int availableSize = GetSizeInByte();
            if (availableSize < msgSize + 4)
            {
                return null;
            }

            bytesList.RemoveAt(0);
            int size = msgBytes.Length;
            // Bytes are not enough
            if (size < msgSize + 4)
            {
                msgBytes = msgBytes.Skip(4).ToArray();
                while (size < msgSize + 4)
                {
                    var nextBytes = bytesList.ElementAt(0);
                    bytesList.RemoveAt(0);
                    size += nextBytes.Length;
                    if (size < msgSize + 4)
                    {
                        // Bytes are not enough
                        msgBytes = msgBytes.Concat(nextBytes).ToArray();
                        continue;
                    }
                    else if (size == msgSize + 4)
                    {
                        // Bytes are exactly enough
                        msgBytes = msgBytes.Concat(nextBytes).ToArray();
                        break;
                    }
                    else
                    {
                        // Bytes are more than requried
                        int countOfBytes = msgSize + 4 - size + nextBytes.Length;
                        msgBytes = msgBytes.Concat(nextBytes.Take(countOfBytes)).ToArray();
                        nextBytes = nextBytes.Skip(countOfBytes).ToArray();
                        bytesList.Insert(0, nextBytes);
                        break;
                    }
                }
            }
            else if (size == msgSize + 4)
            {
                // Bytes are exactly enough
                msgBytes = msgBytes.Skip(4).ToArray();
            }
            else
            {
                // Bytes are more than requried
                int countOfBytes = msgSize + 4;
                var nextBytes = msgBytes;
                msgBytes = msgBytes.Skip(4).Take(msgSize).ToArray();
                nextBytes = nextBytes.Skip(countOfBytes).ToArray();
                bytesList.Insert(0, nextBytes);
            }


            return Message.FromBytes(msgBytes);
        }

        /// <summary>
        /// Get the size of buffered bytes
        /// </summary>
        /// <returns></returns>
        private int GetSizeInByte()
        {
            int size = 0;
            int count = bytesList.Count;
            for (int i = 0; i < count; ++i)
            {
                size += bytesList.ElementAt(i).Length;
            }
            return size;
        }
    }

    public enum MessageStatus
    {
        Success = 0,
        TimedOut = 1,
        LostMessages = 2,
        OtherError = 3
    }

    /// <summary>
    /// A class to store the resultant message and the status of the communication.
    /// </summary>
    public class MessageResult
    {
        static long prevMessageID = -1;
        public static long PrevMessageID
        {
            get
            {
                return prevMessageID;
            }
            set
            {
                prevMessageID = value;
            }
        }

        Message message;
        public Message Message
        {
            get
            {
                return message;
            }
            set
            {
                message = value;
                if (message != null)
                {
                    if (message.MessageID == PrevMessageID + 1)
                    {
                        PrevMessageID++;
                        Status = MessageStatus.Success;
                    }
                }
            }
        }

        public MessageStatus Status
        {
            get;
            set;
        }
    }
}
