using System;
using ch.wuerth.tobias.mux.Core.events;

namespace ch.wuerth.tobias.mux.App
{
    public class ConsoleRethrowCallback : ICallback<Exception>
    {
        public void Push(Exception arg)
        {
            Console.WriteLine("Unhandled exception occurred!");
            Console.WriteLine($"Message: {arg.Message}");
            Console.WriteLine($"Stacktrace: {arg.StackTrace}");
            throw arg;
        }
    }
}