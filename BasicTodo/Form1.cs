using System;
using System.IO;
using System.Windows.Forms;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;



namespace BasicTodo
{
    public partial class Form1 : Form
    {
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BasicTodo");
        private static readonly string DataFile = Path.Combine(DataDir, "todos.txt");

        private const string GroupTodoName = "grpTodo";
        private const string GroupDoneName = "grpDone";

        private bool _loading = false;
        private bool _suppressItemCheck = false; // guard against re-entrancy

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_SHOWME)
                ShowMe();
            base.WndProc(ref m);
        }

        private void ShowMe()
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;

            // Restore + focus
            NativeMethods.ShowWindow(this.Handle, 9); // SW_RESTORE
            NativeMethods.SetForegroundWindow(this.Handle);
            this.Activate();
            this.BringToFront();
        }

        public Form1()
        {
            InitializeComponent();

            //menuStrip1.RenderMode = ToolStripRenderMode.System;

            this.AcceptButton = btnAdd;

            // ListView setup
            lvTodos.View = View.Details;
            lvTodos.HeaderStyle = ColumnHeaderStyle.None;
            lvTodos.CheckBoxes = true;
            lvTodos.ShowGroups = true;
            lvTodos.Sorting = SortOrder.None;
            lvTodos.UseCompatibleStateImageBehavior = false; // safer on XP

            lvTodos.Columns.Clear();
            lvTodos.Columns.Add("Task", lvTodos.ClientSize.Width - 4);
            lvTodos.Resize += lvTodos_Resize;

            // Groups (visible headers as dividers)
            lvTodos.Groups.Clear();
            lvTodos.Groups.Add(new ListViewGroup("Incomplete", HorizontalAlignment.Left) { Name = GroupTodoName });
            lvTodos.Groups.Add(new ListViewGroup("Completed", HorizontalAlignment.Left) { Name = GroupDoneName });

            // Ensure we’re using ItemCheck (not ItemChecked)
            lvTodos.ItemCheck += lvTodos_ItemCheck;

            this.ResizeEnd += (s, e) => lvTodos_Resize(lvTodos, EventArgs.Empty);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Directory.CreateDirectory(DataDir);
            LoadTodos();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveTodos();
        }

        private void txtNew_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnAdd.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var text = txtNew.Text.Trim();
            if (text.Length == 0) return;

            var it = new ListViewItem(text) { Checked = false };
            it.Group = lvTodos.Groups[GroupTodoName];
            lvTodos.Items.Add(it);

            txtNew.Clear();
            SaveTodos();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem sel in lvTodos.SelectedItems)
                lvTodos.Items.Remove(sel);
            SaveTodos();
        }

        // Single handler for toggles
        private void lvTodos_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_loading || _suppressItemCheck) return;

            var item = lvTodos.Items[e.Index];
            bool willBeChecked = (e.NewValue == CheckState.Checked);

            // Defer until ListView commits its own state, then *we* commit and move group
            BeginInvoke((MethodInvoker)(() =>
            {
                if (item.ListView != lvTodos) return;

                _suppressItemCheck = true;  // prevent re-entrancy
                try
                {
                    // Ensure the model state matches what the control just did
                    item.Checked = willBeChecked;
                    item.Group = willBeChecked ? lvTodos.Groups[GroupDoneName]
                                               : lvTodos.Groups[GroupTodoName];
                }
                finally
                {
                    _suppressItemCheck = false;
                }
                SaveTodos();
            }));
        }

        private void LoadTodos()
        {
            _loading = true;
            lvTodos.BeginUpdate();
            try
            {
                lvTodos.Items.Clear();
                if (!File.Exists(DataFile)) return;

                foreach (var line in File.ReadAllLines(DataFile))
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    int i = line.IndexOf('\t'); if (i < 1) continue;
                    bool done = (line.Substring(0, i) == "1");
                    string text = line.Substring(i + 1);

                    var item = new ListViewItem(text);
                    lvTodos.Items.Add(item);

                    // Set both state and group under suppression to avoid firing ItemCheck
                    _suppressItemCheck = true;
                    try
                    {
                        item.Checked = done; // set AFTER add
                        item.Group = lvTodos.Groups[done ? GroupDoneName : GroupTodoName];
                    }
                    finally
                    {
                        _suppressItemCheck = false;
                    }
                }
            }
            finally
            {
                lvTodos.EndUpdate();
                _loading = false;
            }
        }

        private void SaveTodos()
        {
            Directory.CreateDirectory(DataDir);
            using (var w = new StreamWriter(DataFile, false))
            {
                foreach (ListViewItem it in lvTodos.Items)
                {
                    string done = it.Checked ? "1" : "0";
                    w.WriteLine(done + "\t" + it.Text);
                }
            }
        }

        private void lvTodos_Resize(object sender, EventArgs e)
        {
            if (lvTodos.Columns.Count == 0) return;
            BeginInvoke((MethodInvoker)(() =>
            {
                int w = lvTodos.ClientSize.Width - 4;
                if (w < 0) w = 0;
                lvTodos.Columns[0].Width = w;
            }));
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportToMarkdown();
        }

        private void ExportToMarkdown()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Export To-Do List";
                dlg.Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*";
                dlg.FileName = "BasicTodo.md";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("# To-Do List");

                // Export in on-screen order (includes both groups)
                foreach (ListViewItem it in lvTodos.Items)
                {
                    sb.Append(it.Checked ? "- [x] " : "- [ ] ");
                    sb.AppendLine(it.Text);
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
            }
        }

        private void ImportFromMarkdown()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Import To-Do List";
                dlg.Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt|All files (*.*)|*.*";
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                // Exact (ordinal) string comparison for duplicates
                var existing = new HashSet<string>(StringComparer.Ordinal);
                foreach (ListViewItem it in lvTodos.Items)
                    existing.Add(it.Text);

                // Export-compatible line pattern: - [ ] text  OR  - [x] text
                var rx = new Regex(@"^\s*-\s*\[(?<mark>[xX\s])\]\s*(?<text>.+?)\s*$");

                int added = 0;
                _loading = true;
                lvTodos.BeginUpdate();
                try
                {
                    foreach (var line in File.ReadAllLines(dlg.FileName))
                    {
                        var m = rx.Match(line);
                        if (!m.Success) continue;

                        bool done = (m.Groups["mark"].Value.Trim().Equals("x", StringComparison.OrdinalIgnoreCase));
                        string text = m.Groups["text"].Value;
                        if (text.Length == 0) continue;

                        if (existing.Contains(text)) continue; // skip duplicates

                        var item = new ListViewItem(text);
                        lvTodos.Items.Add(item);
                        // Set state and group without triggering re-entrancy logic
                        _suppressItemCheck = true;
                        try
                        {
                            item.Checked = done;
                            item.Group = lvTodos.Groups[done ? GroupDoneName : GroupTodoName];
                        }
                        finally { _suppressItemCheck = false; }

                        existing.Add(text);
                        added++;
                    }
                }
                finally
                {
                    lvTodos.EndUpdate();
                    _loading = false;
                }

                if (added > 0)
                {
                    SaveTodos();
                    // Optional: adjust column width if you have a resizer
                    // lvTodos_Resize(lvTodos, EventArgs.Empty);
                }
                else
                {
                    MessageBox.Show(
                        this,
                        string.Format("No new items found to import."),
                        "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                //MessageBox.Show(
                //    this,
                //    added == 0
                //        ? "No new items found to import."
                //        : string.Format("Imported {0} new item(s).", added),
                //    "Import", 
                //    MessageBoxButtons.OK,
                //    added == 0 ? MessageBoxIcon.Information : MessageBoxIcon.None);

                

            }
        }


        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportFromMarkdown();
        }
    }
}
