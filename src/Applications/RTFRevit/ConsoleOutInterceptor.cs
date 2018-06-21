using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTF.Applications
{
    public interface IConsoleTraceListener
    {
        void OnConsoleOutLine(string text);

        void OnErrorOutLine(string text);
    }

    public class ConsoleOutInterceptor : IDisposable
    {
        LineTextWriter outWriter;
        LineTextWriter errorWriter;
        IConsoleTraceListener listener;

        public ConsoleOutInterceptor(IConsoleTraceListener listener)
        {
            outWriter = new LineTextWriter(OnConsole);
            errorWriter = new LineTextWriter(OnError);
            this.listener = listener;

            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
        }

        private void OnConsole(string line)
        {
            listener?.OnConsoleOutLine(line);
        }

        private void OnError(string line)
        {
            listener?.OnErrorOutLine(line);
        }

        public void Dispose()
        {
            outWriter?.Dispose();
            errorWriter?.Dispose();

            outWriter = null;
            errorWriter = null;
        }
    }

    public class LineTextWriter : TextWriter
    {
        StringBuilder pendingText = new StringBuilder();
        Action<string> callback;

        public LineTextWriter(Action<string> callback)
        {
            this.callback = callback;
        }

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
