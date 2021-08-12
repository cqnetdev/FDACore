using Microsoft.CodeAnalysis;
using Scripting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace FDAScripter
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>

        private enum DB { SQL, PG };

        private static FrmScriptEditor scriptEditor;
        private static frmLogin loginForm;

        private static SqlConnection SQL;
        private static DB ConnectedDBType;

        internal static Dictionary<string, RecentConn> RecentConnections;

        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Scripter.AddNamespace(new string[] { "System" });

            LoadRecentConnections();
            loginForm = new frmLogin();
            Application.Run(loginForm);
        }

        private static void LoadRecentConnections()
        {
            // init the recents dictionaray
            RecentConnections = new Dictionary<string, RecentConn>();

            // get the recent connections from settings (structure is "server,database,user,password;server,database....")
            string recentsString = Properties.Settings.Default.RecentConnections;

            if (!recentsString.Contains(";")) return;

            string[] recentsArray = recentsString.Split(";", StringSplitOptions.RemoveEmptyEntries);
            foreach (string recent in recentsArray)
            {
                RecentConn conn = new(recent);
                RecentConnections.Add(conn.server, conn);
            }
        }

        internal static void ConnectSQL(string instance, string dbname, string user, string password, bool savepwd)
        {
            string connString = "Server=" + instance + "; Database = " + dbname + "; user = " + user + "; password = " + password + ";";

            SQL = new SqlConnection(connString);

            SQL.Open();

            ConnectedDBType = DB.SQL;

            if (savepwd)
                AddToRecent(instance, dbname, user, password);
            else
                AddToRecent(instance, dbname, user);

            scriptEditor = new FrmScriptEditor();
            scriptEditor.FormClosed += ScriptEditor_FormClosed;
            scriptEditor.Show();
            loginForm.Hide();
        }

        private static void ScriptEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            Shutdown();
        }

        private static void Shutdown()
        {
            if (ConnectedDBType == DB.SQL)
            {
                if (SQL.State == ConnectionState.Open)
                {
                    SQL.Close();
                    SQL.Dispose();
                }
            }

            Application.Exit();
        }

        private static void AddToRecent(string instance, string dbname, string user, string password = "")
        {
            if (!RecentConnections.ContainsKey(instance))
            {
                string conn = instance + "," + dbname + "," + user + "," + password;
                RecentConn newconn = new(conn);
                RecentConnections.Add(instance, newconn);

                Properties.Settings.Default.RecentConnections += conn + ";";
                Properties.Settings.Default.Save();
            }
        }

        internal static DataTable QueryDB(string query)
        {
            DataTable result = new();

            string safequery = GetSafeQuery(query);
            if (ConnectedDBType == DB.SQL)
            {
                using (SqlDataAdapter da = new(safequery, SQL))
                {
                    da.Fill(result);
                }
            }

            return result;
        }

        internal static void ExecuteDBquery(string query)
        {
            string safequery = GetSafeQuery(query);
            using (SqlCommand comm = SQL.CreateCommand())
            {
                comm.CommandText = safequery;
                comm.ExecuteNonQuery();
            }
        }

        internal static ImmutableArray<Diagnostic> CheckScript(string code)
        {
            ImmutableArray<Diagnostic> result = Scripter.CheckScript(code);

            return result;
        }

        private static string GetSafeQuery(string unsafeQuery)
        {
            return unsafeQuery;
        }

        internal class RecentConn
        {
            public string server;
            public string database;
            public string user;
            public string pass;

            public RecentConn(string conn)
            {
                string[] connparts = conn.Split(",");
                server = connparts[0];
                database = connparts[1];
                user = connparts[2];
                pass = connparts[3];
            }
        }
    }
}