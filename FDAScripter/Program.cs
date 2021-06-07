using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data.Common;
using System.Data;

namespace FDAScripter
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>

        private enum DB {SQL,PG };

        private static frmScriptEditor scriptEditor;
        private static frmLogin loginForm;

        private static SqlConnection SQL;
        private static DB ConnectedDBType;

        internal static Dictionary<string, RecentConn> RecentConnections;


        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
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

            string[] recentsArray = recentsString.Split(";",StringSplitOptions.RemoveEmptyEntries);
            foreach (string recent in recentsArray)
            {
                RecentConn conn = new RecentConn(recent);
                RecentConnections.Add(conn.server, conn);
            }
        }


        internal static void ConnectSQL(string instance,string dbname,string user,string password,bool savepwd)
        {
            string connString = "Server=" + instance + "; Database = " + dbname + "; user = " + user + "; password = " + password + ";";

            SQL = new SqlConnection(connString);

            SQL.Open();

            ConnectedDBType = DB.SQL;

            if (savepwd)
                AddToRecent(instance, dbname, user, password);
            else
                AddToRecent(instance, dbname, user);


            scriptEditor = new frmScriptEditor();
            scriptEditor.Show();
            loginForm.Hide();          
        }

        private static void AddToRecent(string instance, string dbname, string user, string password="")
        {
            if (!RecentConnections.ContainsKey(instance))
            {
                string conn = instance + "," + dbname + "," + user + "," + password;
                RecentConn newconn = new RecentConn(conn);
                RecentConnections.Add(instance, newconn);

                Properties.Settings.Default.RecentConnections += conn + ";";
                Properties.Settings.Default.Save();
            }
        }

        internal static DataTable QueryDB(string query)
        {
            DataTable result = new DataTable();

            if (ConnectedDBType == DB.SQL)
            {
                using (SqlDataAdapter da = new SqlDataAdapter(query, SQL))
                {
                    da.Fill(result);
                }
            }

            return result;
        }

        internal static void ExecuteDBquery(string query)
        {
            using (SqlCommand comm = SQL.CreateCommand())
            {
                comm.CommandText = query;
                comm.ExecuteNonQuery();
            }
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
