using ShutdownTimer.Helpers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShutdownTimer
{
    public partial class InputBox : Form
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool PasswordMode { get; set; }

        public string ReturnValue { get; set; }

        public InputBox()
        {
            InitializeComponent();
        }

        private void InputBox_Load(object sender, EventArgs e)
        {
            Text = Loc.T("App.Title") + " - " + Title;
            titleLabel.Text = Title;
            messageLabel.Text = Message;
            if (PasswordMode) { inputTextBox.PasswordChar = Convert.ToChar("*"); }

            // 字体按界面语言选择：这个对话框承载密码与自定义命令提示，
            // 上游同样写死了没有中文字形的 Microsoft Sans Serif
            this.Font = Loc.CreateUiFont(8.25f);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.ReturnValue = inputTextBox.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void inputTextBox_TextChanged(object sender, EventArgs e)
        {
            okButton.Enabled = !inputTextBox.Text.Equals("");
        }
    }
}
