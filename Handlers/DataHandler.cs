using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;

namespace CherryBBS
{
    using Parameter = SqlParameter;

    public class DataHandler : IDisposable
    {
        private static readonly byte[] injecTable = {
             0x00, 0x08, 0x09, 0x0a, 0x0d, 0x1a, 0x22,
             0x25, 0x27, 0x5c, 0x5f
        };

        private SqlConnection SqlConnection { get; set; }
        private SqlCommand CommandInstance { get; set; }

        internal static byte[] InjecTable
        {
            get
            {
                return injecTable;
            }
        }

        public static string HashString(ref string source)
        {
            if (source == null) return null;

            var saltString = string.Format("{0}{1}{2}{3}{4}{5}{6}",
                                          (char)108, (char)152, (char)254, source,
                                          (char)233, (char)152, (char)107);

            using (var crypto = new Rfc2898DeriveBytes(saltString, new byte[] {
                    255, 103, 66, 26, 10, 47, 152, 90
                }))
            {
                return Convert.ToBase64String(
                        crypto.GetBytes(32)
                    );
            }
        }

        public static bool ValidateInject(ref string source)
        {
            foreach (char injectChar in InjecTable)
            {
                if (source.Contains(injectChar)) return false;
            }

            return true;
        }

        public DataHandler()
        {
            try
            {
                SqlConnection = new SqlConnection(DataConfig.connectionString);
                SqlConnection.Open();
            }
            catch(SqlException)
            {
                Dispose();
            }
        }

        public void CreateCommand(string procedure,Parameter[] parameterSet=null)
        {
            CommandInstance = new SqlCommand()
            {
                Connection = SqlConnection,
                CommandText = procedure,
                CommandType = CommandType.Text
            };

            if (parameterSet != null)
            {
                foreach (var parameter in parameterSet)
                {
                    if (parameter.Value.GetType() == typeof(string))
                    {
                        var valueString = (string)parameter.Value;

                        foreach (var injectChar in InjecTable)
                        {
                            if (valueString.Contains((char)injectChar))
                            {
                                CommandInstance = null;
                                return;
                            }
                        }
                    }

                    CommandInstance.Parameters.Add(parameter);
                }
            }
        }

        public void ModifyParameter(string paramName, object paramValue)
        {
            if (CommandInstance != null)
            {
                try
                {
                    CommandInstance.Parameters[paramName].Value = paramValue;
                }
                catch
                {
                    CommandInstance.Parameters.Add(new Parameter(paramName, paramValue));
                }
            }
        }

        public bool Executable()
        {
            return !(SqlConnection == null || CommandInstance == null);
        }

        public DataTable Execute()
        {
            if (SqlConnection == null)
                return null;
            else if (CommandInstance == null)
                return null;
            else
            {
                var reader = CommandInstance.ExecuteReader();
                if (reader.HasRows)
                {
                    var dataTable = new DataTable();
                    dataTable.Load(reader);
                    reader.Close();
                    return dataTable;
                }
                else
                {
                    reader.Close();
                    return null;
                }
            }
        }

        public int ExecuteNonQuery()
        {
            if (SqlConnection == null)
                return -1;
            else if (CommandInstance == null)
                return -1;
            else
                return CommandInstance.ExecuteNonQuery();
        }

        public object ExecuteScalar()
        {
            return SqlConnection == null || CommandInstance == null ? null : CommandInstance.ExecuteScalar();
        }

        public void Dispose()
        {
            if (CommandInstance != null)
                CommandInstance = null;

            if (SqlConnection != null)
                try
                {
                    SqlConnection.Close();
                }
                finally { SqlConnection = null; }
        }
    }
}
