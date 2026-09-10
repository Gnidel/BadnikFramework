using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public partial class ErrorLog : RichTextLabel
{
    private class Message
    {
        public string Text;
        public DateTime Timestamp;

        public Message(string txt)
        {
            this.Text = txt;
            this.Timestamp = DateTime.Now;
        }
    }

    private static List<Message> Messages = new List<Message>();
    public double ShowTimeSeconds = 10f;

    public static void AddMessage(string txt)
    {
        Messages.Add(new Message(txt));
    }

    public override void _Process(double delta)
    {
        this.Text = "";
        foreach (var msg in Messages)
        {
            if (msg.Timestamp > DateTime.Now.AddSeconds(ShowTimeSeconds))
            {
                this.Text += msg.Timestamp + " " + msg.Text + "\n";
            }
        }
    }

    public override void _Ready()
    {
        //var logFileLocation = ProjectSettings.GetSetting("debug/file_logging/log_path").AsString();
        //if (!FileAccess.FileExists(logFileLocation)) return;
        //using var file = FileAccess.Open(logFileLocation, FileAccess.ModeFlags.Read);
        //      string content = file.GetAsText();
        //var lines = content.Split("\n", false);
        //for (int i = Math.Max(0, lines.Length-25); i < lines.Length-1; i++ )
        //      {
        //          AddMessage(content);
        //      }
        //file.Close();
    }
}
