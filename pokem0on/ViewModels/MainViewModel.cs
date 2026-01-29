using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace pokem0on.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Random _rnd = new Random();

        public ObservableCollection<Pokemon> Pokemons { get; private set; }
        public ICollectionView PokemonsView { get; private set; }

        public ObservableCollection<string> TypesFilter { get; private set; }
        public ObservableCollection<string> TypesEdit { get; private set; }

        // -------------------- POKEDEX FILTER --------------------
        private string _search = "";
        public string Search
        {
            get { return _search; }
            set { _search = value; OnPropertyChanged(); if (PokemonsView != null) PokemonsView.Refresh(); }
        }

        private string _selectedType = "All";
        public string SelectedType
        {
            get { return _selectedType; }
            set { _selectedType = value; OnPropertyChanged(); if (PokemonsView != null) PokemonsView.Refresh(); }
        }

        private Pokemon _selectedPokemon;
        public Pokemon SelectedPokemon
        {
            get { return _selectedPokemon; }
            set { _selectedPokemon = value; OnPropertyChanged(); }
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; OnPropertyChanged(); }
        }

        // -------------------- COMMANDS --------------------
        public ICommand AddCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand LoadCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }
        public ICommand AddFromWebCommand { get; private set; }

        // ==================== GAME 1: GUESS ====================
        private Pokemon _guessPokemon;
        public Pokemon GuessPokemon
        {
            get { return _guessPokemon; }
            set { _guessPokemon = value; OnPropertyChanged(); }
        }

        private string _guessInput = "";
        public string GuessInput
        {
            get { return _guessInput; }
            set { _guessInput = value; OnPropertyChanged(); }
        }

        private string _guessMessage = "";
        public string GuessMessage
        {
            get { return _guessMessage; }
            set { _guessMessage = value; OnPropertyChanged(); }
        }

        private int _guessScore = 0;
        public string GuessScoreText => "Score: " + _guessScore;

        public ICommand GuessNextCommand { get; private set; }
        public ICommand GuessCheckCommand { get; private set; }
        public ICommand GuessRevealCommand { get; private set; }

        // ==================== GAME 2: TYPE QUIZ ====================
        private Pokemon _typeQuizPokemon;
        public Pokemon TypeQuizPokemon
        {
            get { return _typeQuizPokemon; }
            set { _typeQuizPokemon = value; OnPropertyChanged(); }
        }

        private string _typeOption1, _typeOption2, _typeOption3, _typeOption4;
        public string TypeOption1 { get { return _typeOption1; } set { _typeOption1 = value; OnPropertyChanged(); } }
        public string TypeOption2 { get { return _typeOption2; } set { _typeOption2 = value; OnPropertyChanged(); } }
        public string TypeOption3 { get { return _typeOption3; } set { _typeOption3 = value; OnPropertyChanged(); } }
        public string TypeOption4 { get { return _typeOption4; } set { _typeOption4 = value; OnPropertyChanged(); } }

        private string _typeQuizMessage = "";
        public string TypeQuizMessage
        {
            get { return _typeQuizMessage; }
            set { _typeQuizMessage = value; OnPropertyChanged(); }
        }

        private int _typeQuizScore = 0;
        public string TypeQuizScoreText => "Score: " + _typeQuizScore;

        public ICommand NextTypeQuizCommand { get; private set; }
        public ICommand AnswerTypeCommand { get; private set; } // parameter = chosen type (string)

        // ==================== GAME 3: BATTLE ====================
        private Pokemon _battleLeft;
        public Pokemon BattleLeft
        {
            get { return _battleLeft; }
            set { _battleLeft = value; OnPropertyChanged(); OnPropertyChanged(nameof(BattleLeftStats)); }
        }

        private Pokemon _battleRight;
        public Pokemon BattleRight
        {
            get { return _battleRight; }
            set { _battleRight = value; OnPropertyChanged(); OnPropertyChanged(nameof(BattleRightStats)); }
        }

        private string _battleResult = "Choose two Pokémon and press FIGHT!";
        public string BattleResult
        {
            get { return _battleResult; }
            set { _battleResult = value; OnPropertyChanged(); }
        }

        public string BattleLeftStats =>
            BattleLeft == null ? "" : $"HP {BattleLeft.HP} | Atk {BattleLeft.Attack} | Def {BattleLeft.Defense} | Power {Power(BattleLeft)}";

        public string BattleRightStats =>
            BattleRight == null ? "" : $"HP {BattleRight.HP} | Atk {BattleRight.Attack} | Def {BattleRight.Defense} | Power {Power(BattleRight)}";

        public ICommand FightCommand { get; private set; }
        public ICommand RandomBattleCommand { get; private set; }

        // -------------------- CTOR --------------------
        public MainViewModel()
        {
            TypesFilter = new ObservableCollection<string>(new[]
            {
                "All","Water","Fire","Electric","Grass","Ice","Rock","Ground","Psychic","Normal","Unknown"
            });

            TypesEdit = new ObservableCollection<string>(new[]
            {
                "Water","Fire","Electric","Grass","Ice","Rock","Ground","Psychic","Normal","Unknown"
            });

            Pokemons = new ObservableCollection<Pokemon>();
            PokemonsView = CollectionViewSource.GetDefaultView(Pokemons);
            PokemonsView.Filter = FilterPokemon;

            AddCommand = new RelayCommand(AddPokemon);
            DeleteCommand = new RelayCommand(DeletePokemon);
            SaveCommand = new RelayCommand(SaveToXml);
            LoadCommand = new RelayCommand(LoadFromXml);
            ResetCommand = new RelayCommand(ResetDemo);
            AddFromWebCommand = new AsyncRelayCommand(AddPokemonFromWebAsync);

            // Games
            GuessNextCommand = new RelayCommand(GuessNext);
            GuessCheckCommand = new RelayCommand(GuessCheck);
            GuessRevealCommand = new RelayCommand(GuessReveal);

            NextTypeQuizCommand = new RelayCommand(NextTypeQuiz);
            AnswerTypeCommand = new RelayCommand<string>(AnswerType);

            FightCommand = new RelayCommand(Fight);
            RandomBattleCommand = new RelayCommand(RandomBattle);

            ResetDemo();

            // init games
            GuessNext();
            NextTypeQuiz();
            InitBattleDefaults();
        }

        // -------------------- FILTER --------------------
        private bool FilterPokemon(object obj)
        {
            var p = obj as Pokemon;
            if (p == null) return false;

            if (SelectedType != "All")
            {
                if (string.IsNullOrEmpty(p.Type)) return false;
                if (p.Type.IndexOf(SelectedType, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                string s = Search.Trim();
                bool ok =
                    (!string.IsNullOrEmpty(p.Name) && p.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.Type) && p.Type.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    p.Id.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!ok) return false;
            }

            return true;
        }

        // -------------------- DEMO DATA --------------------
        private void ResetDemo()
        {
            Pokemons.Clear();

            Pokemons.Add(new Pokemon
            {
                Id = 25,
                Name = "Pikachu",
                Type = "Electric",
                HP = 35,
                Attack = 55,
                Defense = 40,
                ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/25.png"
            });

            Pokemons.Add(new Pokemon
            {
                Id = 7,
                Name = "Squirtle",
                Type = "Water",
                HP = 44,
                Attack = 48,
                Defense = 65,
                ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/7.png"
            });

            Pokemons.Add(new Pokemon
            {
                Id = 4,
                Name = "Charmander",
                Type = "Fire",
                HP = 39,
                Attack = 52,
                Defense = 43,
                ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/4.png"
            });

            if (PokemonsView != null) PokemonsView.Refresh();
            if (Pokemons.Count > 0) SelectedPokemon = Pokemons[0];

            StatusText = "Reset demo done";
        }

        // -------------------- CRUD --------------------
        private void AddPokemon()
        {
            int newId = 1;
            foreach (var p in Pokemons)
                if (p.Id >= newId) newId = p.Id + 1;

            var pkm = new Pokemon
            {
                Id = newId,
                Name = "NewPokemon",
                Type = "Unknown",
                HP = 10,
                Attack = 10,
                Defense = 10,
                ImagePath = ""
            };

            Pokemons.Add(pkm);
            SelectedPokemon = pkm;
            if (PokemonsView != null) PokemonsView.Refresh();
            StatusText = "Added new pokemon";
        }

        private void DeletePokemon()
        {
            if (SelectedPokemon == null) return;

            int idx = Pokemons.IndexOf(SelectedPokemon);
            Pokemons.Remove(SelectedPokemon);

            if (Pokemons.Count > 0)
            {
                if (idx < 0) idx = 0;
                if (idx >= Pokemons.Count) idx = Pokemons.Count - 1;
                SelectedPokemon = Pokemons[idx];
            }
            else
            {
                SelectedPokemon = null;
            }

            if (PokemonsView != null) PokemonsView.Refresh();
            StatusText = "Deleted pokemon";
        }

        // -------------------- SAVE / LOAD XML --------------------
        private void SaveToXml()
        {
            try
            {
                var dlg = new SaveFileDialog { Filter = "XML file (*.xml)|*.xml", FileName = "pokemons.xml" };
                if (dlg.ShowDialog() != true) return;

                var list = new List<Pokemon>(Pokemons);
                var ser = new XmlSerializer(typeof(List<Pokemon>));

                using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write))
                    ser.Serialize(fs, list);

                StatusText = "Saved: " + dlg.FileName;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Save error: " + ex.Message);
            }
        }

        private void LoadFromXml()
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "XML file (*.xml)|*.xml" };
                if (dlg.ShowDialog() != true) return;

                var ser = new XmlSerializer(typeof(List<Pokemon>));
                List<Pokemon> list;

                using (var fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read))
                    list = (List<Pokemon>)ser.Deserialize(fs);

                Pokemons.Clear();
                foreach (var p in list) Pokemons.Add(p);

                if (PokemonsView != null) PokemonsView.Refresh();
                if (Pokemons.Count > 0) SelectedPokemon = Pokemons[0];

                StatusText = "Loaded: " + dlg.FileName;

                // refresh games
                GuessNext();
                NextTypeQuiz();
                InitBattleDefaults();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Load error: " + ex.Message);
            }
        }

        // -------------------- WEB ADD (ONE POKEMON BY NAME) --------------------
        private async Task AddPokemonFromWebAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Search))
                {
                    System.Windows.MessageBox.Show("Type a Pokémon name (example: pikachu) and press Enter.");
                    return;
                }

                string query = Search.Trim().ToLowerInvariant();
                StatusText = "Loading from web: " + query;

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("WPF-Pokedex");

                    string url = "https://pokeapi.co/api/v2/pokemon/" + query;
                    string json = await http.GetStringAsync(url);

                    var api = Deserialize<PokeApiPokemon>(json);

                    string type = "Unknown";
                    if (api.types != null && api.types.Length > 0 && api.types[0].type != null)
                        type = Capitalize(api.types[0].type.name);

                    int hp = GetStat(api, "hp");
                    int atk = GetStat(api, "attack");
                    int def = GetStat(api, "defense");

                    string img = api.sprites != null ? api.sprites.front_default : "";

                    foreach (var p in Pokemons)
                    {
                        if (p.Id == api.id)
                        {
                            SelectedPokemon = p;
                            StatusText = "Already exists: " + p.Name;
                            Search = "";
                            return;
                        }
                    }

                    var newPokemon = new Pokemon
                    {
                        Id = api.id,
                        Name = Capitalize(api.name),
                        Type = type,
                        HP = hp,
                        Attack = atk,
                        Defense = def,
                        ImagePath = img
                    };

                    Pokemons.Add(newPokemon);
                    SelectedPokemon = newPokemon;
                    if (PokemonsView != null) PokemonsView.Refresh();

                    StatusText = "Added: " + newPokemon.Name;
                    Search = "";

                    // update games
                    GuessNext();
                    NextTypeQuiz();
                    InitBattleDefaults();
                }
            }
            catch (HttpRequestException)
            {
                System.Windows.MessageBox.Show("Pokémon not found or no internet. Try: pikachu, bulbasaur, mew.");
                StatusText = "Web load failed";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error: " + ex.Message);
                StatusText = "Error";
            }
        }

        private int GetStat(PokeApiPokemon p, string statName)
        {
            if (p.stats == null) return 0;
            for (int i = 0; i < p.stats.Length; i++)
            {
                var s = p.stats[i];
                if (s != null && s.stat != null &&
                    string.Equals(s.stat.name, statName, StringComparison.OrdinalIgnoreCase))
                    return s.base_stat;
            }
            return 0;
        }

        private string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.Length == 1) return s.ToUpper();
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private T Deserialize<T>(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)ser.ReadObject(ms);
        }

        // ==================== GAME 1: GUESS ====================
        private void GuessNext()
        {
            if (Pokemons.Count == 0) return;
            GuessPokemon = Pokemons[_rnd.Next(Pokemons.Count)];
            GuessInput = "";
            GuessMessage = "Type the name and press Enter / Check.";
        }

        private void GuessCheck()
        {
            if (GuessPokemon == null) return;

            string a = (GuessInput ?? "").Trim();
            string b = (GuessPokemon.Name ?? "").Trim();

            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                _guessScore++;
                OnPropertyChanged(nameof(GuessScoreText));
                GuessMessage = "✅ Correct! It was " + GuessPokemon.Name;
                GuessInput = "";
                GuessNext();
            }
            else
            {
                GuessMessage = "❌ Wrong. Try again.";
            }
        }

        private void GuessReveal()
        {
            if (GuessPokemon == null) return;
            GuessMessage = "Answer: " + GuessPokemon.Name;
        }

        // ==================== GAME 2: TYPE QUIZ ====================
        private void NextTypeQuiz()
        {
            if (Pokemons.Count == 0) return;

            TypeQuizPokemon = Pokemons[_rnd.Next(Pokemons.Count)];
            TypeQuizMessage = "Pick the correct type.";

            // Build 4 options: 1 correct + 3 random from TypesEdit
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var options = new List<string>();

            string correct = string.IsNullOrWhiteSpace(TypeQuizPokemon.Type) ? "Unknown" : TypeQuizPokemon.Type;
            set.Add(correct);
            options.Add(correct);

            while (options.Count < 4)
            {
                string t = TypesEdit[_rnd.Next(TypesEdit.Count)];
                if (set.Add(t)) options.Add(t);
            }

            // shuffle
            for (int i = 0; i < options.Count; i++)
            {
                int j = _rnd.Next(i, options.Count);
                var tmp = options[i]; options[i] = options[j]; options[j] = tmp;
            }

            TypeOption1 = options[0];
            TypeOption2 = options[1];
            TypeOption3 = options[2];
            TypeOption4 = options[3];
        }

        private void AnswerType(string chosen)
        {
            if (TypeQuizPokemon == null) return;

            string correct = string.IsNullOrWhiteSpace(TypeQuizPokemon.Type) ? "Unknown" : TypeQuizPokemon.Type;

            if (string.Equals(chosen, correct, StringComparison.OrdinalIgnoreCase))
            {
                _typeQuizScore++;
                OnPropertyChanged(nameof(TypeQuizScoreText));
                TypeQuizMessage = "✅ Correct! " + TypeQuizPokemon.Name + " is " + correct;
                NextTypeQuiz();
            }
            else
            {
                TypeQuizMessage = "❌ Wrong. Correct: " + correct;
            }
        }

        // ==================== GAME 3: BATTLE ====================
        private void InitBattleDefaults()
        {
            if (Pokemons.Count == 0) return;
            BattleLeft = Pokemons[0];
            BattleRight = Pokemons.Count > 1 ? Pokemons[1] : Pokemons[0];
            BattleResult = "Choose two Pokémon and press FIGHT!";
            OnPropertyChanged(nameof(BattleLeftStats));
            OnPropertyChanged(nameof(BattleRightStats));
        }

        private int Power(Pokemon p)
        {
            if (p == null) return 0;
            // simple formula
            return p.HP + p.Attack + p.Defense;
        }

        private void Fight()
        {
            if (BattleLeft == null || BattleRight == null)
            {
                BattleResult = "Select two Pokémon first.";
                return;
            }

            int left = Power(BattleLeft);
            int right = Power(BattleRight);

            if (left > right)
                BattleResult = $"🏆 Winner: {BattleLeft.Name} ({left} vs {right})";
            else if (right > left)
                BattleResult = $"🏆 Winner: {BattleRight.Name} ({right} vs {left})";
            else
                BattleResult = $"🤝 Draw! ({left} vs {right})";

            OnPropertyChanged(nameof(BattleLeftStats));
            OnPropertyChanged(nameof(BattleRightStats));
        }

        private void RandomBattle()
        {
            if (Pokemons.Count == 0) return;
            BattleLeft = Pokemons[_rnd.Next(Pokemons.Count)];
            BattleRight = Pokemons[_rnd.Next(Pokemons.Count)];
            if (Pokemons.Count > 1)
            {
                while (BattleRight == BattleLeft)
                    BattleRight = Pokemons[_rnd.Next(Pokemons.Count)];
            }
            Fight();
        }

        // -------------------- INotifyPropertyChanged --------------------
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }

    // ==================== MODEL ====================
    public class Pokemon : INotifyPropertyChanged
    {
        private int _id;
        public int Id { get { return _id; } set { _id = value; OnPropertyChanged(); } }

        private string _name;
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(); } }

        private string _type;
        public string Type { get { return _type; } set { _type = value; OnPropertyChanged(); } }

        private int _hp;
        public int HP { get { return _hp; } set { _hp = value; OnPropertyChanged(); } }

        private int _attack;
        public int Attack { get { return _attack; } set { _attack = value; OnPropertyChanged(); } }

        private int _defense;
        public int Defense { get { return _defense; } set { _defense = value; OnPropertyChanged(); } }

        private string _imagePath;
        public string ImagePath { get { return _imagePath; } set { _imagePath = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }

    // ==================== COMMANDS ====================
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { _execute(); }
        public event EventHandler CanExecuteChanged;
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter)
        {
            if (parameter == null) { _execute(default(T)); return; }
            _execute((T)parameter);
        }
        public event EventHandler CanExecuteChanged;
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private bool _isRunning;

        public AsyncRelayCommand(Func<Task> executeAsync) { _executeAsync = executeAsync; }

        public bool CanExecute(object parameter) { return !_isRunning; }

        public async void Execute(object parameter)
        {
            if (_isRunning) return;
            _isRunning = true;
            try { await _executeAsync(); }
            finally
            {
                _isRunning = false;
                var h = CanExecuteChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public event EventHandler CanExecuteChanged;
    }

    // ==================== DTO for PokeAPI ====================
    [DataContract]
    public class PokeApiPokemon
    {
        [DataMember] public int id;
        [DataMember] public string name;
        [DataMember] public PokeSprites sprites;
        [DataMember] public PokeTypeSlot[] types;
        [DataMember] public PokeStatSlot[] stats;
    }

    [DataContract]
    public class PokeSprites
    {
        [DataMember] public string front_default;
    }

    [DataContract]
    public class PokeTypeSlot
    {
        [DataMember] public PokeType type;
    }

    [DataContract]
    public class PokeType
    {
        [DataMember] public string name;
    }

    [DataContract]
    public class PokeStatSlot
    {
        [DataMember] public int base_stat;
        [DataMember] public PokeStat stat;
    }

    [DataContract]
    public class PokeStat
    {
        [DataMember] public string name;
    }
}
