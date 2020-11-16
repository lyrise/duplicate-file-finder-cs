using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using Cocona;
using DuplicateFileFinder;
using DuplicateFileFinder.Models;
using System.Threading.Tasks;

namespace DuplicateFileFinder
{
    class Program
    {
        static void Main(string[] args)
        {
            CoconaLiteApp.Run<Program>(args);
        }

        public async Task Find([Option('d')] bool delete = false)
        {
            try
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var finder = new DuplicateFileFinder(currentDirectory);
                await finder.FindAndDeleteAsync(delete);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
