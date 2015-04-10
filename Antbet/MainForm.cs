using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Json;
using System.Threading;

namespace Antbet
{
	public partial class MainForm : Form
	{
		HttpWebRequest request;
		HttpWebResponse response;
		string userURL = "https://api.primedice.com/api/users/1?access_token=" + Properties.Settings.Default.access_token;

		bool KeepBetting = false;
		bool doubleBetOnLoss = false;
		bool doubleBetOnWin = false;
		int consecutiveLosses = 0;
		int consecutiveWins = 0;

        double startAmount;
		double betAmount;
		double currentBalance;

		BettingThread betting;
		Thread bettingThread;
		ThreadStart bettingThreadStart;

        double totalProfit = 0.0;
        double profitSinceReset = 0.0;
        int wins = 0;
        int losses = 0;

        int seedChangeCounter = 0;

		public MainForm()
		{
			InitializeComponent();
			getBalance();
            startAmount = currentBalance;
			betting = new BettingThread(this);
			bettingThreadStart = new ThreadStart(betting.placeBets);
			bettingThread = new Thread(bettingThreadStart);
		}

		public void getBalance()
		{
			request = (HttpWebRequest)WebRequest.Create(userURL);

			request.Method = "GET";
			request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";


			try
			{
				response = (HttpWebResponse)request.GetResponse();
				string message = new StreamReader(response.GetResponseStream()).ReadToEnd();
				//MessageBox.Show(message);

				JsonValue j = JsonValue.Parse(message);

				double balance = (double)j["user"]["balance"];
				currentBalance = balance;
				balance = balance / 100000000;
				BalanceLabel.Text = balance.ToString("0.00000000");
			}
			catch (WebException ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		public void makeBet()
		{

			if (InvokeRequired)
			{
				MethodInvoker method = new MethodInvoker(makeBet);
				Invoke(method);
				return;
			}

            betting.betMade(false);


			if (doubleBetOnLoss)
			{
				betAmount += betAmount * int.Parse(IncreaseOnLossTextBox.Text)/100;
			}
			else if (doubleBetOnWin)
			{
				betAmount += betAmount * int.Parse(doubleOnWinTextBox.Text) / 100;
			} else
			{
				Double.TryParse(BetAmountTextBox.Text, out betAmount);
				betAmount *= 100000000;
				int betAmountConv = (int)betAmount;
			}

            if (currentBalance < betAmount)
            {
                MessageBox.Show("Insufficient funds!");
                betting.betMade(true);
                bettingThread.Suspend();
                doubleBetOnLoss = false;
                doubleBetOnWin = false;
                StartRollingButton.Text = "Start Rolling";
                consecutiveLosses = 0;
                return;
            }
            request = (HttpWebRequest)WebRequest.Create("https://api.primedice.com/api/bet?access_token=" + Properties.Settings.Default.access_token);

			
			string postData = "amount=" + betAmount + "&target=" + BetMultiplierTextBox.Text + "&condition=<";
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
				//MessageBox.Show(message);
				JsonValue j = JsonValue.Parse(message);

				string id = (string)j["bet"]["id"];

				double amount;
				Double.TryParse((string)j["bet"]["amount"], out amount);
                amount /= 100000000;

				float roll;
				float.TryParse((string)j["bet"]["roll"], out roll);

				float multiplier;
				float.TryParse((string)j["bet"]["target"], out multiplier);

				double profit;
				Double.TryParse((string)j["bet"]["profit"], out profit);
				double originalProfit = profit;
				profit /= 100000000;
                profitSinceReset += profit;

				bool win = false;
				if ((string)j["bet"]["win"] == "true")
					win = true;

                BetResultsDataGrid.Rows.Insert(0, id, amount.ToString("0.00000000"), roll.ToString(), multiplier.ToString() + "%", profit.ToString("0.00000000"));
				if (win)
				{
					BetResultsDataGrid.Rows[0].DefaultCellStyle.ForeColor = Color.DarkSeaGreen;
					BetResultsDataGrid.Rows[0].DefaultCellStyle.BackColor = Color.ForestGreen;
                    doubleBetOnLoss = false;
					if(int.Parse(doubleOnWinTextBox.Text) != 0)
						doubleBetOnWin = true;
					consecutiveLosses = 0;
					consecutiveWins++;
                    wins++;

				}
				else
				{
					BetResultsDataGrid.Rows[0].DefaultCellStyle.ForeColor = Color.White;
					BetResultsDataGrid.Rows[0].DefaultCellStyle.BackColor = Color.Red;

					if(int.Parse(IncreaseOnLossTextBox.Text) != 0)
						doubleBetOnLoss = true;
                    doubleBetOnWin = false;
					consecutiveLosses++;
					consecutiveWins = 0;
                    losses++;
                    seedChangeCounter++;

					if (seedChangeCounter >= int.Parse(changeSeedTextBox.Text) && int.Parse(changeSeedTextBox.Text) != 0)
                    {
                        seedChangeCounter = 0;
                        SetSeed();
                    }

				}

                if (consecutiveLosses >= int.Parse(StopAfterXTextBox.Text) && int.Parse(StopAfterXTextBox.Text) != 0)
				{
					bettingThread.Suspend();
					doubleBetOnLoss = false;
					StartRollingButton.Text = "Start Rolling";
                    consecutiveLosses = 0;
					MessageBox.Show("Stopped Betting!");
				}

				if (consecutiveWins >= int.Parse(ResetAfterXWinsTextBox.Text) && int.Parse(ResetAfterXWinsTextBox.Text) != 0)
				{
					Double.TryParse(BetAmountTextBox.Text, out betAmount);
					betAmount *= 100000000;
					int betAmountConv = (int)betAmount;
				}
				if (consecutiveLosses >= int.Parse(ResetAfterXLossesTextBox.Text) && int.Parse(ResetAfterXLossesTextBox.Text) != 0)
				{
					Double.TryParse(BetAmountTextBox.Text, out betAmount);
					betAmount *= 100000000;
					int betAmountConv = (int)betAmount;
				}

                if (profitSinceReset > Double.Parse(ResetAfterXProfitTextBox.Text) && Double.Parse(ResetAfterXProfitTextBox.Text) != 0)   
                {
                    profitSinceReset = 0.0;

                    Double.TryParse(BetAmountTextBox.Text, out betAmount);
                    betAmount *= 100000000;
                    int betAmountConv = (int)betAmount;
                }
				currentBalance += originalProfit;
                totalProfit += originalProfit;

                if (currentBalance > (double.Parse(WithdrawLimit.Text) * 100000000) && WithdrawCheckBox.Checked)
					Withdraw();

				checkAmountOfRows();

				double balance = currentBalance;
				balance = balance / 100000000;
				
                BalanceLabel.Text = balance.ToString("0.00000000") + " BTC";

                betting.betMade(true);

                if (totalProfit < 0)
                    ProfitLabel.ForeColor = Color.Red;
                else if (totalProfit == 0)
                    ProfitLabel.ForeColor = Color.Yellow;
                else
                    ProfitLabel.ForeColor = Color.Green;
                ProfitLabel.Text = ((double)totalProfit / 100000000).ToString("0.00000000") + " BTC";
			}
			catch (WebException ex)
			{
				
                KeepBetting = false;
				betting.betMade(true);
                bettingThread.Suspend();
                StartRollingButton.Text = "Start Rolling";

				Double.TryParse(BetAmountTextBox.Text, out betAmount);
				betAmount *= 100000000;
				int betAmountConv = (int)betAmount;
			}
		}

		public void getMultiplier()
		{
			double mult;
			Double.TryParse(BetMultiplierTextBox.Text, out mult);
			ChanceToWinLabel.Text = "Multiplier: " + (99 / mult).ToString("#0.00") + "x";
		}

		public void MainForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			betting.RequestStop();

			if (bettingThread.IsAlive)
				bettingThread.Resume();
			else
				bettingThread.Start();

			Application.Exit();
		}

		public void label6_Click(object sender, EventArgs e)
		{

		}

		public void StartRollingButton_Click(object sender, EventArgs e)
		{
			if (!KeepBetting)
			{
				StartRollingButton.Text = "Stop Rolling";
				KeepBetting = true;
				if (bettingThread.IsAlive)
					bettingThread.Resume();
				else
					bettingThread.Start();

			} else {
				KeepBetting = false;
				bettingThread.Suspend();
				StartRollingButton.Text = "Start Rolling";

				Double.TryParse(BetAmountTextBox.Text, out betAmount);
				betAmount *= 100000000;
				int betAmountConv = (int)betAmount;
			}
		}

		public void BetMultiplierTextBox_TextChanged(object sender, EventArgs e)
		{
			getMultiplier();
		}

		public void checkAmountOfRows()
		{
			if (BetResultsDataGrid.Rows.Count > 30)
			{
				BetResultsDataGrid.Rows.RemoveAt(30);
			}
		}

		private void Withdraw()
		{
			request = (HttpWebRequest)WebRequest.Create("https://api.primedice.com/api/withdraw?access_token=" + Properties.Settings.Default.access_token);

			double withDrawamount = Double.Parse(WithdrawAmount.Text);
			withDrawamount *= 100000000;
			string postData = "amount=" + withDrawamount.ToString("############") + "&address=" + WithdrawAddress.Text;
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
				currentBalance -= withDrawamount;
			}
			catch (WebException ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

        private void SetSeed()
        {
            betting.seedChange(true);
            request = (HttpWebRequest)WebRequest.Create("https://api.primedice.com/api/seed?access_token=" + Properties.Settings.Default.access_token);

            string postData = "seed=" + GetRandomSeed();
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
                betting.seedChange(false);
            }
            catch (WebException ex)
            {
                //FunctionStatus.Text = ex.ToString();
                betting.seedChange(false);
            }

            
        }

		private void martingale2xToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "100";
			BetAmountTextBox.Text = "0.00000001";
			BetMultiplierTextBox.Text = "49.50";

		}

		private void the90StratToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "900";
			BetAmountTextBox.Text = "0.00000010";
			BetMultiplierTextBox.Text = "90";
		}

		private void xTheMoneyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "50";
			BetAmountTextBox.Text = "0.00000002";
			BetMultiplierTextBox.Text = "20";
		}

		private void oneAndAHalfToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "300";
			BetAmountTextBox.Text = "0.0000002";
			BetMultiplierTextBox.Text = "66";
		}

		private void aSolid35ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "100";
			BetAmountTextBox.Text = "0.00000002";
			BetMultiplierTextBox.Text = "35.42";
		}

		private void theSwagToolStripMenuItem_Click(object sender, EventArgs e)
		{
			IncreaseOnLossTextBox.Text = "200";
			BetAmountTextBox.Text = "0.00000005";
			BetMultiplierTextBox.Text = "84";
			doubleOnWinTextBox.Text = "5";
			ResetAfterXWinsTextBox.Text = "4";
			ResetAfterXLossesTextBox.Text = "3";
			StopAfterXTextBox.Text = "400";
		}

		private void WithdrawLimit_TextChanged(object sender, EventArgs e)
		{

		}

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            getBalance();
        }

        private void StopBettingThread()
        {

            betting.betMade(true);
            betting.RequestStop();
            if (bettingThread.ThreadState != ThreadState.Running)
            {
                bettingThread.Start();
            }
            StartRollingButton.Text = "Start Autobet";
        }

        private string GetRandomSeed()
        {
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new string(
                Enumerable.Repeat(chars, 30)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());

            return result;
        }

        private void ResetAfterXWinsTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void label16_Click(object sender, EventArgs e)
        {

        }

        private void BetResultsDataGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

		

	}
}
