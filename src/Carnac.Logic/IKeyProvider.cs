using System;
using Carnac.Logic.Models;
using System.Reactive.Subjects;

namespace Carnac.Logic
{
    public interface IKeyProvider
    {
        ISubject<KeyPress, KeyPress> GetKeyStream();
    }
}