using CN_Project;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CN_Project_Server
{
    public partial class ServerWindow : Form
    {
        #region Variable Definitions
        private bool isMyTurn = true;
        private static char[,] grid = new char[3, 3];
        private static Dictionary<Control, bool> isButtonLocked = new Dictionary<Control, bool>();
        private static Control[,] ButtonGrid = new Control[3, 3];
        private static string cellSelected = "  ";
        private static char mySymbol = 'X';
        private static char opponentSymbol = 'O';
        private static Socket listenerSocket;
        private static Socket clientSocket;

        public delegate void labelDelegate(Color labelColor, string labelText);
        public delegate void cellDelegate(RadioButton button, string text);
        public delegate void controlsDelegate(bool status);
        #endregion

        public ServerWindow()
        {
            InitializeComponent();

            foreach (Control table in Controls.OfType<TableLayoutPanel>())
            {
                foreach (RadioButton button in table.Controls)
                {
                    isButtonLocked.Add(button, false);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    string buttonName = "Button" + i.ToString() + "_" + j.ToString();
                    Control[] button = Controls.Find(buttonName, true);
                    ButtonGrid[i, j] = button[0];
                }
            }
        }

        #region Labels and Port Box
        private void PortTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)))
            {
                e.Handled = true;
            }
        }

        public void ChangeStatus(Color labelColor, string labelText)
        {
            if (labelColor == Color.DarkGreen)
                EnableOrDisableControls(true);

            ConnectionStatusLabel.BackColor = labelColor;
            ConnectionStatusLabel.Text = labelText;
        }

        public void ChangeTurn(Color labelColor, string labelText)
        {
            TurnLabel.Text = labelText;
            TurnLabel.ForeColor = labelColor;
        }
        #endregion

        #region Networking
        public void ReceiveMessages()
        {
            clientSocket = listenerSocket.Accept();
            ConnectionStatusLabel.BeginInvoke(new labelDelegate(ChangeStatus), Color.DarkGreen, "Connected");
            listenerSocket.Close();

            while (true)
            {
                try
                {
                    byte[] recvBuffer = new byte[clientSocket.SendBufferSize];
                    int bytesReceived = clientSocket.Receive(recvBuffer);
                    Array.Resize(ref recvBuffer, bytesReceived);

                    string recvMessage = Encoding.ASCII.GetString(recvBuffer);
                    int x, y;
                    x = recvMessage.ElementAt(0) - '0';
                    y = recvMessage.ElementAt(1) - '0';
                    SelectCell(x, y, opponentSymbol);
                    isMyTurn = true;
                    cellSelected = "  ";

                    TurnLabel.BeginInvoke(new labelDelegate(ChangeTurn), Color.Crimson, "Turn: Yours");

                    if (CheckWinner(opponentSymbol))
                    {
                        MatchWonWindow matchWindow = new MatchWonWindow("Oops! You lost the game!", new List<string>(), Color.DeepSkyBlue);
                        matchWindow.ShowDialog();
                        ResetGame();
                        continue;
                    }

                    if (CheckDraw())
                    {
                        MatchWonWindow matchWindow = new MatchWonWindow("It's a draw!", new List<string>(), Color.White);
                        matchWindow.ShowDialog();
                        ResetGame();
                        continue;
                    }
                }
                catch
                {
                    ResetGame();
                    ConnectionStatusLabel.BeginInvoke(new labelDelegate(ChangeStatus), Color.Red, "Disconnected");
                    this.BeginInvoke(new controlsDelegate(EnableOrDisableControls), false);
                    clientSocket.Close();
                    break;
                }
            }
        }

        private void ListenBtn_Click(object sender, EventArgs e)
        {
            int port;
            Int32.TryParse(PortTextBox.Text, out port);

            if (port < 0 || (port > 0 && port < 1024))
            {
                MessageBox.Show("Invalid port entered, please enter another. Allowed range is above port 1024.");
                return;
            }

            listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listenerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                listenerSocket.Listen(0);
            }
            catch
            {
                MessageBox.Show("This port is already in use, please enter another. Allowed range is above port 1024.");
                return;
            }

            ChangeStatus(Color.Yellow, "Awaiting Connection");

            Thread thread = new Thread(ReceiveMessages);
            thread.IsBackground = true;
            thread.Start();
        }

        private void SendBtn_Click(object sender, EventArgs e)
        {
            if (!isMyTurn)
            {
                MessageBox.Show("It is the opponent's turn!");
                return;
            }

            if (cellSelected == "  ")
            {
                MessageBox.Show("You haven't selected any cell!");
                return;
            }

            if (clientSocket == null) return;

            byte[] sendData = Encoding.ASCII.GetBytes(cellSelected + opponentSymbol);
            clientSocket.Send(sendData);
            cellSelected = "  ";
            isMyTurn = false;
            ChangeTurn(Color.DeepSkyBlue, "Turn: Opponent's");

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    RadioButton button = (RadioButton)ButtonGrid[i, j];

                    if (button.Checked && !isButtonLocked[button])
                    {
                        isButtonLocked[button] = true;
                        button.ForeColor = Color.Crimson;
                        grid[i, j] = mySymbol;
                    }
                }
            }

            if (CheckWinner(mySymbol))
            {
                MatchWonWindow matchWindow = new MatchWonWindow("Congratulations! You won the game!", new List<string>(), Color.Crimson);
                matchWindow.ShowDialog();
                ResetGame();
                return;
            }

            if (CheckDraw())
            {
                MatchWonWindow matchWindow = new MatchWonWindow("It's a draw!", new List<string>(), Color.White);
                matchWindow.ShowDialog();
                ResetGame();
                return;
            }
        }

        private void DisconnectBtn_Click(object sender, EventArgs e)
        {
            ResetGame();
            if (clientSocket == null) return;
            EnableOrDisableControls(false);
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            ChangeStatus(Color.Red, "Disconnected");
        }
        #endregion

        #region Game Logic
        private void ResetGame()
        {
            isMyTurn = true;
            cellSelected = "  ";

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    grid[i, j] = ' ';
                    isButtonLocked[ButtonGrid[i, j]] = false;
                    ButtonGrid[i, j].Tag = "false";
                    ButtonGrid[i, j].ForeColor = Color.White;
                    ButtonGrid[i, j].BeginInvoke(new cellDelegate(ClearButtonText), ButtonGrid[i, j], "");
                }
            }

            if (TurnLabel.InvokeRequired)
                TurnLabel.BeginInvoke(new labelDelegate(ChangeTurn), Color.Crimson, "Turn: Yours");
            else
                ChangeTurn(Color.Crimson, "Turn: Yours");
        }

        private void EnableOrDisableControls(bool status)
        {
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    ButtonGrid[i, j].Enabled = status;

            SendBtn.Enabled = status;
            TurnLabel.Visible = status;

            ListenBtn.Enabled = !status;
            PortTextBox.Enabled = !status;
        }

        private void MarkCell(Control button, string cellText)
        {
            button.Text = cellText;
            button.ForeColor = cellText == mySymbol.ToString() ? Color.Crimson : Color.DeepSkyBlue;
        }

        private void SelectCell(int x, int y, char player)
        {
            Control buttonToChange = ButtonGrid[x, y];

            if (isButtonLocked[buttonToChange])
                return;

            if (player == opponentSymbol)
            {
                isButtonLocked[buttonToChange] = true;
                grid[x, y] = player;
            }

            if (cellSelected != "  ")
            {
                int oldX = cellSelected.ElementAt(0) - '0';
                int oldY = cellSelected.ElementAt(1) - '0';
                Control previousButton = ButtonGrid[oldX, oldY];
                previousButton.Tag = "false";
            }

            buttonToChange.Tag = "true";

            if (buttonToChange.InvokeRequired)
                buttonToChange.BeginInvoke(new cellDelegate(MarkCell), buttonToChange, player.ToString());
            else
                MarkCell(buttonToChange, player.ToString());

            cellSelected = x.ToString() + y.ToString();
        }

        private void ResetUncheckedButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    RadioButton button = (RadioButton)ButtonGrid[i, j];
                    if (!button.Checked && !isButtonLocked[ButtonGrid[i, j]])
                    {
                        button.ForeColor = Color.White;
                        button.Text = "";
                    }
                }
            }
        }

        private void ClearButtonText(RadioButton button, string text)
        {
            button.Text = text;
        }

        private bool CheckDraw()
        {
            return !isButtonLocked.ContainsValue(false);
        }

        private bool CheckWinner(char player)
        {
            // Horizontal checks
            for (int i = 0; i < 3; i++)
            {
                if (grid[i, 0] == player && grid[i, 1] == player && grid[i, 2] == player)
                    return true;
            }

            // Vertical checks
            for (int i = 0; i < 3; i++)
            {
                if (grid[0, i] == player && grid[1, i] == player && grid[2, i] == player)
                    return true;
            }

            // Diagonals
            if (grid[0, 0] == player && grid[1, 1] == player && grid[2, 2] == player)
                return true;

            if (grid[0, 2] == player && grid[1, 1] == player && grid[2, 0] == player)
                return true;

            return false;
        }
        #endregion

        #region Button Click Handlers
        private void Button0_0_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(0, 0, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button0_1_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(0, 1, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button0_2_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(0, 2, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button1_0_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(1, 0, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button1_1_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(1, 1, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button1_2_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(1, 2, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button2_0_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(2, 0, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button2_1_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(2, 1, mySymbol);
            ResetUncheckedButtons();
        }

        private void Button2_2_Click(object sender, EventArgs e)
        {
            if (!isMyTurn) return;
            SelectCell(2, 2, mySymbol);
            ResetUncheckedButtons();
        }
        #endregion

        #region Button Hover Handlers
        private void HoverButton(int x, int y, Color borderColor, int borderSize, char buttonText)
        {
            if (!isMyTurn) return;

            RadioButton buttonToChange = (RadioButton)ButtonGrid[x, y];
            if ((string)buttonToChange.Tag == "false")
                buttonToChange.Text = buttonText.ToString();

            buttonToChange.FlatAppearance.BorderColor = borderColor;
            buttonToChange.FlatAppearance.BorderSize = borderSize;
        }

        private void Button0_0_MouseEnter(object sender, EventArgs e) => HoverButton(0, 0, Color.Crimson, 3, mySymbol);
        private void Button0_0_MouseLeave(object sender, EventArgs e) => HoverButton(0, 0, Color.White, 1, ' ');
        private void Button0_1_MouseEnter(object sender, EventArgs e) => HoverButton(0, 1, Color.Crimson, 3, mySymbol);
        private void Button0_1_MouseLeave(object sender, EventArgs e) => HoverButton(0, 1, Color.White, 1, ' ');
        private void Button0_2_MouseEnter(object sender, EventArgs e) => HoverButton(0, 2, Color.Crimson, 3, mySymbol);
        private void Button0_2_MouseLeave(object sender, EventArgs e) => HoverButton(0, 2, Color.White, 1, ' ');
        private void Button1_0_MouseEnter(object sender, EventArgs e) => HoverButton(1, 0, Color.Crimson, 3, mySymbol);
        private void Button1_0_MouseLeave(object sender, EventArgs e) => HoverButton(1, 0, Color.White, 1, ' ');
        private void Button1_1_MouseEnter(object sender, EventArgs e) => HoverButton(1, 1, Color.Crimson, 3, mySymbol);
        private void Button1_1_MouseLeave(object sender, EventArgs e) => HoverButton(1, 1, Color.White, 1, ' ');
        private void Button1_2_MouseEnter(object sender, EventArgs e) => HoverButton(1, 2, Color.Crimson, 3, mySymbol);
        private void Button1_2_MouseLeave(object sender, EventArgs e) => HoverButton(1, 2, Color.White, 1, ' ');
        private void Button2_0_MouseEnter(object sender, EventArgs e) => HoverButton(2, 0, Color.Crimson, 3, mySymbol);
        private void Button2_0_MouseLeave(object sender, EventArgs e) => HoverButton(2, 0, Color.White, 1, ' ');
        private void Button2_1_MouseEnter(object sender, EventArgs e) => HoverButton(2, 1, Color.Crimson, 3, mySymbol);
        private void Button2_1_MouseLeave(object sender, EventArgs e) => HoverButton(2, 1, Color.White, 1, ' ');
        private void Button2_2_MouseEnter(object sender, EventArgs e) => HoverButton(2, 2, Color.Crimson, 3, mySymbol);
        private void Button2_2_MouseLeave(object sender, EventArgs e) => HoverButton(2, 2, Color.White, 1, ' ');
        #endregion
    }
}