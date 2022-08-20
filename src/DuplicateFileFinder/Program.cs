using Cocona;

namespace DuplicateFileFinder
{
    class Program : CoconaLiteConsoleAppBase
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

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
                await finder.FindAndDeleteAsync(delete, this.Context.CancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }
        }
    }
}
