using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace pokem0on
{
    public class Pokemon : INotifyPropertyChanged
    {
        private int _id;
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _type;
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }

        private int _hp;
        public int HP { get => _hp; set { _hp = value; OnPropertyChanged(); } }

        private int _attack;
        public int Attack { get => _attack; set { _attack = value; OnPropertyChanged(); } }

        private int _defense;
        public int Defense { get => _defense; set { _defense = value; OnPropertyChanged(); } }

        private string _imagePath;
        public string ImagePath { get => _imagePath; set { _imagePath = value; OnPropertyChanged(); } }

        private string _description;
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
