using System;
using System.Linq;
using System.Text;
using ch.wuerth.tobias.mux.Core.exceptions;
using ch.wuerth.tobias.mux.Core.logging;
using ch.wuerth.tobias.mux.Core.plugin;
using ch.wuerth.tobias.mux.Core.processor;
using ch.wuerth.tobias.mux.Data;
using ch.wuerth.tobias.mux.Data.models;
using Microsoft.EntityFrameworkCore;

namespace ch.wuerth.tobias.mux.plugins.PluginUserMgmt
{
    public class PluginUserMgmt : PluginBase
    {
        public PluginUserMgmt(LoggerBundle logger) : base("user", logger) { }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            RegisterAction("add", AddUser);
        }

        protected override void Process(String[] args)
        {
            if (args.Length.Equals(0))
            {
                TriggerAction("help");
                return;
            }

            TriggerActions(args.ToList());
        }

        private void AddUser()
        {
            try
            {
                // read username
                Logger?.Information?.Log("Enter a username");
                String username = Console.ReadLine()?.Trim() ?? "";

                if (String.IsNullOrWhiteSpace(username))
                {
                    Logger?.Exception?.Log(new ArgumentException("Username cannot be empty"));
                    return;
                }

                // check existance
                Logger?.Information?.Log("Checking if user already exists...");
                Boolean exists;
                using (DataContext dataContext = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    exists = dataContext.SetUsers.Any(x => x.Username.ToLower().Equals(username.ToLower()));
                }
                if (exists)
                {
                    Logger?.Exception?.Log(new ArgumentException("Username already exists"));
                    return;
                }

                Logger?.Information?.Log("User not found database. Allowed to proceed forward");

                // get password
                Logger?.Information?.Log("Enter a password");
                String pw1 = ReadPassword();
                Logger?.Information?.Log("Confirm password");
                String pw2 = ReadPassword();

                if (!pw1.Equals(pw2))
                {
                    Logger?.Exception?.Log(new ArgumentException("Passwords do not match"));
                    return;
                }

                // hash password
                PasswordProcessor pp = new PasswordProcessor();
                (String hashedPass, Boolean success) = pp.Handle(pw1, Logger);
                if (!success)
                {
                    Logger?.Exception?.Log(new ProcessAbortedException());
                    return;
                }

                // save model
                User user = new User
                {
                    Username = username
                    , Password = hashedPass
                };
                using (DataContext dataContext = new DataContext(new DbContextOptions<DataContext>(), Logger))
                {
                    dataContext.SetUsers.Add(user);
                    dataContext.SaveChanges();
                }
                Logger?.Information?.Log($"User '{user.Username}' created with unique identifier '{user.UniqueId}'");
            }
            catch (Exception ex)
            {
                Logger?.Exception?.Log(ex);
            }
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

        protected override void OnActionHelp(StringBuilder sb)
        {
            sb.Append($"Usage: app {Name} <action>");
            sb.Append(Environment.NewLine);
            sb.Append("Actions: add");
        }
    }
}