using System;
using System.IO;
using System.Text;

namespace RTF.Applications
{
    /// <summary>
    /// Interface to implement to get console/error out text
    /// </summary>
    public interface IConsoleTraceListener
    {
        /// <summary>
        /// Called with each complete line received on Console.Out
        /// </summary>
        /// <param name="text">line of text</param>
        void OnConsoleOutLine(string text);

        /// <summary>
        /// Called with each complete line received on Error.Out
        /// </summary>
        /// <param name="text">line of text</param>
        void OnErrorOutLine(string text);
    }

    /// <summary>
    /// Helper class to redirect and intercept Console Out and Error Out streams
    /// This is used to shuffle all console messages from RTF client to server
    /// </summary>
    public class ConsoleOutInterceptor : IDisposable
    {
        private LineTextWriter outWriter;
        private LineTextWriter errorWriter;
        private IConsoleTraceListener listener;

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="listener">object that implements <see cref="IConsoleTraceListener"/> 
        /// which will receive callbacks for each line of output intercepted</param>
        public ConsoleOutInterceptor(IConsoleTraceListener listener)
        {
            outWriter = new LineTextWriter(OnConsole);
            errorWriter = new LineTextWriter(OnError);
            this.listener = listener;

            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
        }

        /// <summary>
        /// Callback for LineTextWriter. 
        /// Called when a line of text is received from the Console.Out stream
        /// </summary>
        /// <param name="line">line of text</param>
        private void OnConsole(string line)
        {
            listener?.OnConsoleOutLine(line);
        }

        /// <summary>
        /// Callback for LineTextWriter. 
        /// Called when a line of text is received from the Console.Error stream
        /// </summary>
        /// <param name="line">line of text</param>
        private void OnError(string line)
        {
            listener?.OnErrorOutLine(line);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                outWriter?.Dispose();
                errorWriter?.Dispose();

                outWriter = null;
                errorWriter = null;
            }
        }
    }

    /// <summary>
    /// TextWriter that produces a callback after each line of text
    /// </summary>
    public class LineTextWriter : TextWriter
    {
        private StringBuilder pendingText = new StringBuilder();
        private Action<string> callback;

        public LineTextWriter(Action<string> callback)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Write a char to the pending buffer
        /// This is the only overload we need since that's all that Console streams call
        /// </summary>
        public override void Write(char value)
        {
            base.Write(value);
            if (value != '\n')
            {
                if (value == '\r')
                {
                    string line = GetPendingLine();
                    callback?.Invoke(line);
                }
                else
                {
                    pendingText.Append(value);
                }
            }
        }

        public override Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

        private string GetPendingLine()
        {
            string line = pendingText.ToString();
            pendingText.Clear();

            return line;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                string line = GetPendingLine();

                if (!string.IsNullOrEmpty(line))
                {
                    callback?.Invoke(line);
                }
            }
        }
    }
}
