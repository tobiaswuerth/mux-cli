using System;
using System.Linq;
using System.Text;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Core.processing;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;

namespace ch.wuerth.tobias.mux.plugins.PluginUserMgmt
{
    public class PluginUserMgmt : PluginBase
    {
        public PluginUserMgmt() : base("user") { }

        private void AddUser()
        {
            LoggerBundle.Debug("Starting process to add new user...");
            try
            {
                // read username
                LoggerBundle.Inform(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, "Enter a username: ");
                String username = Console.ReadLine()?.Trim() ?? "";

                if (String.IsNullOrWhiteSpace(username))
                {
                    LoggerBundle.Fatal(new ArgumentException("Username cannot be empty"));
                    Environment.Exit(1);
                }

                // check existance
                LoggerBundle.Debug("Checking if user already exists...");
                Boolean exists;
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    exists = dataContext.SetUsers.Any(x => x.Username.ToLower().Equals(username.ToLower()));
                }
                if (exists)
                {
                    LoggerBundle.Fatal(new ArgumentException("Username already exists"));
                    Environment.Exit(1);
                }

                LoggerBundle.Trace("User not found database. Allowed to proceed forward");

                // get password
                LoggerBundle.Inform(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, "Enter a password: ");
                String pw1 = ReadPassword();
                LoggerBundle.Inform(Logger.DefaultLogFlags & ~LogFlags.SuffixNewLine, "Confirm password: ");
                String pw2 = ReadPassword();

                if (!pw1.Equals(pw2))
                {
                    LoggerBundle.Fatal(new ArgumentException("Passwords do not match"));
                    Environment.Exit(1);
                }

                // hash password
                Sha512HashPipe hashPipe = new Sha512HashPipe();
                String hashedPw = hashPipe.Process(pw1);

                // save model
                User user = new User
                {
                    Username = username
                    , Password = hashedPw
                };
                using (DataContext dataContext = DataContextFactory.GetInstance())
                {
                    dataContext.SetUsers.Add(user);
                    dataContext.SaveChanges();
                }
                LoggerBundle.Inform(
                    $"Successfully created user '{user.Username}' created with unique identifier '{user.UniqueId}'");
            }
            catch (Exception ex)
            {
                LoggerBundle.Error(ex);
            }
        }

        protected override String GetHelp()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Usage: app {Name} <action>");
            sb.Append(Environment.NewLine);
            sb.Append("Actions: add | Create new user");
            return sb.ToString();
        }

        protected override void OnInitialize()
        {
            LoggerBundle.Debug($"Initializing plugin '{Name}'...");
            RegisterAction("add", AddUser);
        }

        protected override void Process(String[] args)
        {
            OnProcessStarting();

            if (args.Length.Equals(0))
            {
                TriggerAction("help");
                return;
            }

            TriggerActions(args.ToList());
        }

        private static String ReadPassword()
        {
            StringBuilder sb = new StringBuilder();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                // Ignore any key out of range.
                if ((Int32) key.Key < 32 || (Int32) key.Key > 126)
                {
                    continue;
                }

                sb.Append(key.KeyChar);
            }
            while (key.Key != ConsoleKey.Enter);

            return sb.ToString();
        }
    }
}