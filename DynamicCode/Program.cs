using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Threading;
using DynamicCode;
using Common;

namespace sandbox
{
  
    public class Program
    {
        private class usercodeFile
        {
            public string Name;
            public byte[] Hash;

            public usercodeFile(string name, byte[] hash)
            {
                Name = name;
                Hash = hash;
            }
        }

        static Dictionary<string, Tag> tags;
        static Random rnd = new Random();
        private static Timer tag2ChangeTimer;
        private static Timer tag3ChangeTimer;

        static Dictionary<string, usercodeFile> LoadedCodeFiles = new Dictionary<string, usercodeFile>();


        static void Main(string[] args)
        {
            tags = new Dictionary<string, SubscribeableObject>();
            tags.Add("tag1", new Tag("tag1", 0, DateTime.Now, 192));
            tags.Add("tag2", new Tag("tag2", 0, DateTime.Now, 192)); // updates with random value every 20 seconds
            tags.Add("tag3", new Tag("tag3", 0, DateTime.Now, 192)); // updates with random value every 6 seconds
            tags.Add("tag4", new Tag("tag4", 0, DateTime.Now, 192));
            tags.Add("tag5", new Tag("tag5", 0, DateTime.Now, 192));

            DynamicCodeManager.UserAccessibleObjects = tags;
            DynamicCodeManager.NameSpaces = new string[] { "Common" };
            DynamicCodeManager.UserMethodExecuted += DynamicCode_UserMethodExecuted;

            tag2ChangeTimer = new Timer(ValueChangeTimerTick, "tag2", 20000, 20000);
            tag3ChangeTimer = new Timer(ValueChangeTimerTick, "tag3", 6000, 6000);


            while (true)
            {
                LoadUserCode();
                Thread.Sleep(5000);
            }
        }

        private static void DynamicCode_UserMethodExecuted(string methodName)
        {
            Console.WriteLine("Executing user method '" + methodName + "'");
        }

        private static void ValueChangeTimerTick(object o)
        {
            string tagid = (string)o;
            tags[tagid].Value = rnd.NextDouble() * 1000;
            tags[tagid].Quality = 192;
            tags[tagid].Timestamp = DateTime.Now;
        }

        private static void LoadUserCode()
        {
            // get a list of all .cs files in the current folder
            string[] csfiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.cs");

            // check if any currently loaded modules are not in the file list (have been deleted)
            foreach (string loadedFile in LoadedCodeFiles.Keys)
            {
                if (!csfiles.Contains(loadedFile))
                {
                    Console.WriteLine("user code module " + loadedFile + " not found, unloading it");

                    // this file has been deleted, unload it from our dynamic code
                    UnloadModule(loadedFile);
                }
            }


            // check if any of the files in the file list are not in our currently loaded file list (new code files)
            foreach (string filename in csfiles)
            {
                if (!LoadedCodeFiles.ContainsKey(filename))
                {
                    Console.WriteLine("new user code module " + filename + " found, loading");

                    // this is a new file, load it into dynamic code
                    LoadModule(filename);
                }
            }



            // compare the checksums of files found in the folder to loaded files (different checksum = file has been changed)         
            byte[] fileHash;
            byte[] loadedFileHash;
            foreach (string file in csfiles)
            {
                if (!LoadedCodeFiles.ContainsKey(file))
                    continue;

                fileHash = ComputeHash(file);
                loadedFileHash = LoadedCodeFiles[file].Hash;

                if (!fileHash.SequenceEqual(loadedFileHash))
                {
                    Console.WriteLine("user code module " + file + " has been modified, unloading and reloading it");
                    // unload
                    UnloadModule(file);

                    // reload
                    LoadModule(file);
                }
            }
        }

        static void UnloadModule(string name)
        {
            DynamicCodeManager.UnloadModule(name);
            LoadedCodeFiles.Remove(name);
        }

        static void LoadModule(string filename)
        {
            string userCode = File.ReadAllText(filename);

            try
            {
                List<Tuple<string,string>> loadedMethods = DynamicCodeManager.LoadModule(filename, userCode);
                Console.WriteLine("Compilation successful");
                foreach (Tuple<string, string> method in loadedMethods)
                {
                    Console.WriteLine("User Method '" + filename + "." + method.Item1 + "' loaded (" + method.Item2 + ")");
                }
                LoadedCodeFiles.Add(filename, new usercodeFile(filename, ComputeHash(filename)));
            }
            catch (Exception ex)
            {

                Console.Write("Failed to load module '" + filename + "': ");
                if (ex.GetType() == typeof(DynamicCode.CompileException))
                {
                    Console.WriteLine("Compiler error");
                    foreach (var diagnostic in ((DynamicCode.CompileException)ex).CompileResult)
                    {
                        Console.Error.WriteLine(diagnostic.Id + ": " + diagnostic.GetMessage());
                    }
                }
                else
                    Console.WriteLine(ex.Message);
            }



        }

        static byte[] ComputeHash(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return md5.ComputeHash(stream);
                }
            }
        }

    }
}
