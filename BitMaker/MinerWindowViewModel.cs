using System.ComponentModel;

using BitMaker.Miner;

namespace BitMaker
{

    public class MinerWindowViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public MinerHost Engine { get; set; }

    }

}
