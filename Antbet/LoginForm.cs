using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Json;
using System.Net.Mail;

namespace Antbet
{

    public partial class LoginForm : Form
    {

        public string username = "";
        public string password = "";

        public string loginurl = "https://api.primedice.com/api/login";

        private HttpWebRequest request;
        private HttpWebResponse response;

        public string loginToken = "";

        public LoginForm()
        {
            InitializeComponent();
            username = Properties.Settings.Default.username;
            password = Properties.Settings.Default.password;
            UsernameTextBox.Text = username;
            PasswordTextBox.Text = password;

            if (username != "")
                RememberMeCheckBox.Checked = true;
        }

        private void login()
        {
            request = (HttpWebRequest)WebRequest.Create(loginurl);

            string postData = "username=" + username + "&password=" + password;
            byte[] data = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.ContentLength = data.Length;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            try
            {
                response = (HttpWebResponse)request.GetResponse();
                string message = new StreamReader(response.GetResponseStream()).ReadToEnd();
                JsonValue j = JsonValue.Parse(message);
                loginToken = j["access_token"].ToString();
                loginToken = loginToken.Replace("\"", "");

                if (RememberMeCheckBox.Checked)
                {
                    Properties.Settings.Default.username = username;
                    Properties.Settings.Default.password = password;
                }

                Properties.Settings.Default.access_token = loginToken;
                Properties.Settings.Default.Save();
                this.Hide();

                MainForm mfrm = new MainForm();
                mfrm.Show();

            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            login();
        }

        private void UsernameTextBox_TextChanged(object sender, EventArgs e)
        {
            username = UsernameTextBox.Text;
        }

        private void PasswordTextBox_TextChanged(object sender, EventArgs e)
        {
            password = PasswordTextBox.Text;
        }

        private void PasswordTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.Enter)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {

        }

        private void RememberMeCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }
