using System;
using System.Windows.Input;

namespace KPatcher.Models
{
    /// <summary>
    /// Stubby ICommand implemetation to pass my actions inside target UI
    /// </summary>
    public class UICommand : ICommand
    {
        private readonly Action<object> _action;

        public UICommand(Action<object> action) => _action = action;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _action(parameter);


        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}