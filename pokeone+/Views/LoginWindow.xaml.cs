﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Poke1Bot;


namespace pokeone_plus
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        #region DRAG_MOVE
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        #endregion


        private BotClient _bot;
        public bool ShowAccounts { get; set; }

        public string Username => UsernameTextBox.Text.Trim();

        public string Password => PasswordTextBox.Password;

        public bool HasProxy => ProxyCheckBox.IsChecked.Value;
        public int ProxyVersion
        {
            set
            {
                switch (value)
                {
                    case 4:
                        Socks4RadioButton.IsChecked = true;
                        break;
                    case 5:
                        Socks5RadioButton.IsChecked = true;
                        break;
                }
            }
            get => Socks4RadioButton.IsChecked.Value ? 4 : 5;
        }

        public string ProxyHost => ProxyHostTextBox.Text.Trim();

        public int ProxyPort { get; private set; }

        public string ProxyUsername => ProxyUsernameTextBox.Text.Trim();

        public string ProxyPassword => ProxyPasswordTextBox.Password;
        public LoginWindow(BotClient bot)
        {
            InitializeComponent();

            _bot = bot;

            RefreshAccountList();
            ProxyCheckBox_Checked(null, null);

            Title = App.Name + " - " + Title;
            UsernameTextBox.Focus();
            if (AccountListView.Items.Count > 0)
                ShowAccounts_Click(null, null);
        }

        public void RefreshAccountList()
        {
            IEnumerable<Account> accountList;
            lock (_bot)
            {
                accountList = _bot.AccountManager.Accounts.Values.OrderBy(e => e.Name);
            }
            List<string> accountListView = new List<string>();
            foreach (Account account in accountList)
            {
                accountListView.Add(account.FileName);
            }

            AccountListView.ItemsSource = accountListView;
            AccountListView.Items.Refresh();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (Username.Length == 0)
            {
                UsernameTextBox.Focus();
                return;
            }
            if (Password.Length == 0)
            {
                PasswordTextBox.Focus();
                return;
            }

            if (HasProxy)
            {
                int port;
                if (int.TryParse(ProxyPortTextBox.Text.Trim(), out port) && port >= 0 && port <= 65535)
                {
                    ProxyPort = port;
                    DialogResult = true;
                }
            }
            else
            {
                DialogResult = true;
            }
        }

        private void ProxyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            Visibility hasProxy = ProxyCheckBox.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
            Visibility isSocks5 = ProxyCheckBox.IsChecked.Value && Socks5RadioButton.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
            Visibility hasAuth = ProxyCheckBox.IsChecked.Value && Socks5RadioButton.IsChecked.Value && !AnonymousCheckBox.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
            if (ProxyTypePanel != null)
            {
                ProxyTypePanel.Visibility = hasProxy;
            }
            if (ProxyHostLabel != null)
            {
                ProxyHostLabel.Visibility = hasProxy;
            }
            if (ProxyHostTextBox != null)
            {
                ProxyHostTextBox.Visibility = hasProxy;
            }
            if (ProxyPortLabel != null)
            {
                ProxyPortLabel.Visibility = hasProxy;
            }
            if (ProxyPortTextBox != null)
            {
                ProxyPortTextBox.Visibility = hasProxy;
            }
            if (AnonymousCheckBox != null)
            {
                AnonymousCheckBox.Visibility = isSocks5;
            }
            if (ProxyUsernameLabel != null)
            {
                ProxyUsernameLabel.Visibility = hasAuth;
            }
            if (ProxyPasswordLabel != null)
            {
                ProxyPasswordLabel.Visibility = hasAuth;
            }
            if (ProxyUsernameTextBox != null)
            {
                ProxyUsernameTextBox.Visibility = hasAuth;
            }
            if (ProxyPasswordTextBox != null)
            {
                ProxyPasswordTextBox.Visibility = hasAuth;
            }
        }

        private void ShowAccounts_Click(object sender, RoutedEventArgs e)
        {
            ShowAccounts = !ShowAccounts;
            if (ShowAccounts)
            {
                AccountList.Visibility = Visibility.Visible;
                AccountList.Width = 150;
                ShowAccountsButton.Content = "<";
            }
            else
            {
                AccountList.Width = 0;
                AccountList.Visibility = Visibility.Hidden;
                ShowAccountsButton.Content = ">";
            }
        }

        private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountListView.SelectedItem == null)
            {
                return;
            }
            string accountName = AccountListView.SelectedItem.ToString();
            lock (_bot)
            {
                if (_bot.AccountManager.Accounts.ContainsKey(accountName))
                {
                    Account account = _bot.AccountManager.Accounts[accountName];
                    UsernameTextBox.Text = account.Name;
                    if (account.Password == null)
                    {
                        PasswordTextBox.Clear();
                    }
                    else
                    {
                        PasswordTextBox.Password = account.Password;
                    }
                    if (account.Socks.Version != SocksVersion.None || account.Socks.Username != null || account.Socks.Password != null
                        || account.Socks.Host != null || account.Socks.Port != -1)
                    {
                        ProxyCheckBox.IsChecked = true;
                    }
                    else
                    {
                        ProxyCheckBox.IsChecked = false;
                    }
                    if (account.Socks.Version == SocksVersion.Socks4)
                    {
                        ProxyVersion = 4;
                    }
                    else if (account.Socks.Version == SocksVersion.Socks5)
                    {
                        ProxyVersion = 5;
                    }
                    if (account.Socks.Host == null)
                    {
                        ProxyHostTextBox.Clear();
                    }
                    else
                    {
                        ProxyHostTextBox.Text = account.Socks.Host;
                    }
                    if (account.Socks.Port == -1)
                    {
                        ProxyPortTextBox.Clear();
                    }
                    else
                    {
                        ProxyPortTextBox.Text = account.Socks.Port.ToString();
                    }
                    if (account.Socks.Username != null || account.Socks.Password != null)
                    {
                        AnonymousCheckBox.IsChecked = false;
                    }
                    else
                    {
                        AnonymousCheckBox.IsChecked = true;
                    }
                    if (account.Socks.Username == null)
                    {
                        ProxyUsernameTextBox.Clear();
                    }
                    else
                    {
                        ProxyUsernameTextBox.Text = account.Socks.Username;
                    }
                    if (account.Socks.Password == null)
                    {
                        ProxyPasswordTextBox.Clear();
                    }
                    else
                    {
                        ProxyPasswordTextBox.Password = account.Socks.Password;
                    }
                }
            }
        }
        private void SaveAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShowAccounts)
            {
                ShowAccounts_Click(null, null);
            }
            if (UsernameTextBox.Text == null || UsernameTextBox.Text.Trim() == "")
            {
                return;
            }
            Account account = new Account(UsernameTextBox.Text.Trim());

            if (PasswordTextBox.Password != "" && PasswordTextBox.Password != null)
            {
                account.Password = PasswordTextBox.Password;
            }

            if (HasProxy)
            {
                SocksVersion socksVersion = SocksVersion.None;
                if (ProxyVersion == 4)
                {
                    socksVersion = SocksVersion.Socks4;
                }
                else if (ProxyVersion == 5)
                {
                    socksVersion = SocksVersion.Socks5;
                }
                account.Socks.Version = socksVersion;
                if (ProxyHostTextBox.Text != null && ProxyHostTextBox.Text.Trim() != "")
                {
                    account.Socks.Host = ProxyHostTextBox.Text.Trim();
                }
                if (ProxyPortTextBox.Text != null && ProxyPortTextBox.Text.Trim() != "")
                {
                    int port;
                    if (int.TryParse(ProxyPortTextBox.Text.Trim(), out port))
                    {
                        account.Socks.Port = port;
                    }
                }
                if (ProxyUsernameTextBox.Text != null && ProxyUsernameTextBox.Text.Trim() != "")
                {
                    account.Socks.Username = ProxyUsernameTextBox.Text.Trim();
                }
                if (ProxyPasswordTextBox.Password != null && ProxyPasswordTextBox.Password != "")
                {
                    account.Socks.Password = ProxyPasswordTextBox.Password;
                }
            }
            lock (_bot)
            {
                _bot.AccountManager.Accounts[account.Name] = account;
                _bot.AccountManager.SaveAccount(account.Name);
            }
            RefreshAccountList();
        }
        private void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountListView.SelectedItem == null)
            {
                return;
            }
            string name = AccountListView.SelectedItem.ToString();
            lock (_bot)
            {
                if (_bot.AccountManager.Accounts.ContainsKey(name))
                {
                    _bot.AccountManager.DeleteAccount(name);
                    RefreshAccountList();
                }
            }
        }
    }
}
