﻿using UserInterface.Views;
using Model.Core;

namespace UserInterface.Commands
{
    class RunCommand : ICommand
    {
        private ISimulation Simulation;
        private Simulations Simulations;
        public bool ok { get; set; }

        public RunCommand(Simulations Simulations, ISimulation Simulation)
        {
            this.Simulations = Simulations;
            this.Simulation = Simulation;
        }

        /// <summary>
        /// Perform the command
        /// </summary>
        public void Do(CommandHistory CommandHistory)
        {
            if (Simulation == null)
                Simulations.Run();
            else
                ok = Simulations.Run(Simulation);
        }

        /// <summary>
        /// Undo the command
        /// </summary>
        public void Undo(CommandHistory CommandHistory)
        {
        }

    }
}
