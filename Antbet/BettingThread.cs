using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Antbet
{
	class BettingThread
	{
		MainForm main;
		bool _shouldStop = false;
        public bool previousBetMade = false;
        public bool seedChanging = false;

		public BettingThread(MainForm mnfrm)
		{
			main = mnfrm;
		}

		public void placeBets()
		{
			while (true) 
			{
				if(_shouldStop)
					return;

                main.makeBet();

                //Wait for the last bet to be made before we issue another command.
                //Could potentially cause a frozen thread...
                while (!previousBetMade) ;

                //while (!seedChanging) ;

                Thread.Sleep(400);
			}
		}

		public void RequestStop()
		{
			_shouldStop = true;
		}

        public void betMade(bool val)
        {
            previousBetMade = val;
        }

        public void seedChange(bool val)
        {
            seedChanging = val;
        }
	}
}
