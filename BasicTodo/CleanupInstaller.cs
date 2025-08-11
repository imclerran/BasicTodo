using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Windows.Forms;

namespace BasicTodo
{
    [RunInstaller(true)]
    public class CleanupInstaller : Installer
    {
        public override void Uninstall(IDictionary savedState)
        {
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BasicTodo");

            // Optional prompt (only when there’s an interactive UI)
            bool remove = false;
            if (Environment.UserInteractive)
            {
                var res = MessageBox.Show(
                    "Remove your personal data from this computer?\n\n" +
                    "This will permanently delete all todo list items.",
                    "BasicTodo — Remove data?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                remove = (res == DialogResult.Yes);
            }
            else
            {
                // Silent/basic uninstall: default behavior (keep data)
                remove = false;
            }

            if (remove && Directory.Exists(dataDir))
                Directory.Delete(dataDir, true);

            base.Uninstall(savedState);
        }
    }
}
