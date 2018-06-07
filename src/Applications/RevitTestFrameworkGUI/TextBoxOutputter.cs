using System;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RTF.Applications
{
    public class TextBoxOutputter : TextWriter
    {
        RichTextBox textBox = null;
        StringBuilder pendingText = new StringBuilder();

        public TextBoxOutputter(RichTextBox output)
        {
            textBox = output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            if (value != '\n')
            {
                pendingText.Append(value);

                if (value == '\r')
                {
                    string line = pendingText.ToString();
                    pendingText.Clear();

                    textBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TextRange tr = new TextRange(textBox.Document.ContentEnd, textBox.Document.ContentEnd);
                        tr.Text = $"{DateTime.Now.ToString("HH:mm:ss")} ";
                        tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.DarkGray);

                        tr = new TextRange(textBox.Document.ContentEnd, textBox.Document.ContentEnd);
                        tr.Text = line;
                        if (line.ToLower().StartsWith("error"))
                        {
                            tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);
                        }
                        else if (line.ToLower().StartsWith("warning"))
                        {
                            tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Orange);
                        }
                        else
                        {
                            tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);
                        }
                        textBox.ScrollToEnd();
                    }));
                }
            }
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
