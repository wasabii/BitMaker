using System.ComponentModel;

using BitMaker.Miner;

namespace BitMaker
{

    public class MinerWindowViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public Engine Engine { get; set; }

    }

}
