using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.ComponentModel;

// needs reference: System.Web.Extensions
using System.Web.Script.Serialization;

namespace pokem0on
{
    public partial class MainWindow : Window
    {
        // ===================== DATA =====================
        private ObservableCollection<Pokemon> _pokemons = new ObservableCollection<Pokemon>();
        private ICollectionView _pokemonsView;
        private Random _rng = new Random();

        // ===================== GAME 1: GUESS =====================
        private Pokemon _guessTarget;
        private int _guessScore = 0;
        private int _guessTotal = 0;

        // ===================== GAME 2: TYPE QUIZ =====================
        private Pokemon _typeQuizTarget;
        private int _typeScore = 0;
        private int _typeTotal = 0;

        // ===================== BATTLE =====================
        private class BattleUnit
        {
            public Pokemon BaseData;
            public double CurrentHP;
            public double MaxHP;
            public int UltPoints;
            public int HealPotions;
            public Image UnitImage;
            public string Name;
        }

        private BattleUnit Player1;
        private BattleUnit Player2;

        private bool isPlayer1Turn = true;
        private bool isBattleActive = false;

        private int _round = 1;
        private int _p1RoundsWon = 0;
        private int _p2RoundsWon = 0;

        public MainWindow()
        {
            InitializeComponent();

            SetupPokedex();
            ResetDemo();

            SetupGames();
            SetupBattleSelectors();
        }

        // ============================================================
        // ===================== POKEDEX CORE =========================
        // ============================================================

        private void SetupPokedex()
        {
            _pokemonsView = CollectionViewSource.GetDefaultView(_pokemons);
            _pokemonsView.Filter = FilterPokemon;

            PokemonGrid.ItemsSource = _pokemonsView;

            _pokemons.CollectionChanged += delegate
            {
                RefreshTypeFilter();
                _pokemonsView.Refresh();
            };

            RefreshTypeFilter();
            StatusText.Text = "Ready.";
        }

        private bool FilterPokemon(object obj)
        {
            Pokemon p = obj as Pokemon;
            if (p == null) return false;

            string search = (SearchBox != null ? SearchBox.Text : "");
            if (search == null) search = "";
            search = search.Trim();

            string type = (TypeFilter != null ? TypeFilter.SelectedItem as string : "All");
            if (string.IsNullOrWhiteSpace(type)) type = "All";

            bool okSearch = true;
            if (!string.IsNullOrWhiteSpace(search))
            {
                okSearch = (p.Name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            bool okType = true;
            if (!string.IsNullOrWhiteSpace(type) && type != "All")
            {
                okType = string.Equals(p.Type ?? "", type, StringComparison.OrdinalIgnoreCase);
            }

            return okSearch && okType;
        }

        private void RefreshTypeFilter()
        {
            List<string> types = _pokemons
                .Select(x => (x.Type ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            types.Insert(0, "All");

            string prev = TypeFilter.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(prev)) prev = "All";

            TypeFilter.ItemsSource = types;

            if (types.Contains(prev))
                TypeFilter.SelectedItem = prev;
            else
                TypeFilter.SelectedItem = "All";
        }

        private void UpdateDetails(Pokemon p)
        {
            if (p == null)
            {
                DetailsName.Text = "";
                DetailsType.Text = "";
                DetailsDesc.Text = "";
                DetailsStats.Text = "";
                DetailsImage.Source = null;
                return;
            }

            DetailsName.Text = p.Name ?? "";
            DetailsType.Text = p.Type ?? "";
            DetailsDesc.Text = p.Description ?? "";
            DetailsStats.Text = "HP: " + p.HP + "   |   ATK: " + p.Attack + "   |   DEF: " + p.Defense;
            DetailsImage.Source = LoadImage(p.ImagePath);
        }

        private BitmapImage LoadImage(string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return null;

            try
            {
                if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    // Для URL - загружаем асинхронно
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(pathOrUrl, UriKind.Absolute);
                    bmp.EndInit();
                    if (bmp.CanFreeze) bmp.Freeze();
                    return bmp;
                }
                else if (File.Exists(pathOrUrl))
                {
                    // Локальный файл
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(pathOrUrl, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadImage error: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // ===================== POKEDEX EVENTS =======================
        // ============================================================

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _pokemonsView.Refresh();
        }

        private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _pokemonsView.Refresh();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string name = (SearchBox.Text ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    await AddPokemonFromWebByName(name);
                }
            }
        }

        private void PokemonGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDetails(PokemonGrid.SelectedItem as Pokemon);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            int newId = _pokemons.Any() ? _pokemons.Max(x => x.Id) + 1 : 1;

            Pokemon p = new Pokemon
            {
                Id = newId,
                Name = "NewPokemon",
                Type = "Normal",
                HP = 50,
                Attack = 50,
                Defense = 50,
                ImagePath = "",
                Description = "Custom Pokémon"
            };

            _pokemons.Add(p);
            PokemonGrid.SelectedItem = p;
            PokemonGrid.ScrollIntoView(p);
            StatusText.Text = "Added new row (ID " + newId + "). Edit in table.";
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Pokemon p = PokemonGrid.SelectedItem as Pokemon;
            if (p == null) return;

            if (MessageBox.Show("Delete " + p.Name + "?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _pokemons.Remove(p);
                UpdateDetails(null);
                StatusText.Text = "Deleted.";
            }
        }

        private void ResetDemo_Click(object sender, RoutedEventArgs e)
        {
            ResetDemo();
        }

        private void ResetDemo()
        {
            _pokemons.Clear();

            _pokemons.Add(new Pokemon { Id = 25, Name = "Pikachu", Type = "Electric", HP = 35, Attack = 55, Defense = 40, ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/25.png", Description = "A classic Electric-type Pokémon." });
            _pokemons.Add(new Pokemon { Id = 7, Name = "Squirtle", Type = "Water", HP = 44, Attack = 48, Defense = 65, ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/7.png", Description = "A small Water-type turtle Pokémon." });
            _pokemons.Add(new Pokemon { Id = 4, Name = "Charmander", Type = "Fire", HP = 39, Attack = 52, Defense = 43, ImagePath = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/4.png", Description = "A Fire-type lizard Pokémon." });

            StatusText.Text = "Reset demo done.";
            RefreshTypeFilter();

            if (_pokemons.Count > 0)
            {
                PokemonGrid.SelectedItem = _pokemons[0];
                UpdateDetails(_pokemons[0]);
            }

            SetupGames();
            SetupBattleSelectors();
        }

        // ============================================================
        // ===================== WEB ADD (POKEAPI) =====================
        // ============================================================

        private async void AddFromWeb_Click(object sender, RoutedEventArgs e)
        {
            string name = (SearchBox.Text).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Type pokemon name in Search box (e.g. pikachu) then click Add (web).");
                return;
            }

            await AddPokemonFromWebByName(name);
        }

        private async Task AddPokemonFromWebByName(string name)
        {
            try
            {
                StatusText.Text = "Downloading: " + name + " ...";
                Pokemon p = await FetchPokemonFromPokeApi(name);

                if (p == null)
                {
                    StatusText.Text = "Not found.";
                    MessageBox.Show("Pokemon not found in PokeAPI.");
                    return;
                }

                Pokemon existing = _pokemons.FirstOrDefault(x =>
                    x.Id == p.Id || string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.Name = p.Name;
                    existing.Type = p.Type;
                    existing.HP = p.HP;
                    existing.Attack = p.Attack;
                    existing.Defense = p.Defense;
                    existing.ImagePath = p.ImagePath;
                    existing.Description = p.Description;

                    StatusText.Text = "Updated: " + p.Name;
                    PokemonGrid.SelectedItem = existing;
                    UpdateDetails(existing);
                }
                else
                {
                    _pokemons.Add(p);
                    StatusText.Text = "Added: " + p.Name;
                    PokemonGrid.SelectedItem = p;
                    PokemonGrid.ScrollIntoView(p);
                    UpdateDetails(p);
                }

                SetupGames();
                SetupBattleSelectors();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Web add failed.";
                MessageBox.Show("Web add error:\n" + ex.Message);
            }
        }

        private async void Import151_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Import Gen1 (1..151) from PokeAPI? This can take a while.", "Import 151", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            StatusText.Text = "Import 151 started...";
            SearchBox.Text = "";
            TypeFilter.SelectedItem = "All";

            int added = 0;
            int updated = 0;

            for (int id = 1; id <= 151; id++)
            {
                StatusText.Text = "Importing " + id + "/151 ...";

                Pokemon p = null;
                try
                {
                    p = await FetchPokemonFromPokeApi(id.ToString());
                }
                catch
                {
                    // ignore
                }

                if (p == null) continue;

                Pokemon existing = _pokemons.FirstOrDefault(x => x.Id == p.Id);
                if (existing != null)
                {
                    existing.Name = p.Name;
                    existing.Type = p.Type;
                    existing.HP = p.HP;
                    existing.Attack = p.Attack;
                    existing.Defense = p.Defense;
                    existing.ImagePath = p.ImagePath;
                    existing.Description = p.Description;
                    updated++;
                }
                else
                {
                    _pokemons.Add(p);
                    added++;
                }

                await Task.Delay(25);
            }

            StatusText.Text = "Import finished. Added: " + added + ", Updated: " + updated + ".";
            SetupGames();
            SetupBattleSelectors();
        }

        private static string DownloadString(string url)
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers.Add("User-Agent", "PokedexWpfApp");
                return wc.DownloadString(url);
            }
        }

        private async Task<Pokemon> FetchPokemonFromPokeApi(string nameOrId)
        {
            string url = "https://pokeapi.co/api/v2/pokemon/" + nameOrId.ToLower().Trim() + "/";

            string json = await Task.Run(() => DownloadString(url));

            JavaScriptSerializer jss = new JavaScriptSerializer();
            var root = jss.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) return null;

            int id = Convert.ToInt32(root["id"]);
            string name = Capitalize(root["name"] != null ? root["name"].ToString() : "");

            // type
            string type = "Normal";
            if (root.ContainsKey("types"))
            {
                object[] typesArr = root["types"] as object[];
                if (typesArr != null && typesArr.Length > 0)
                {
                    var t0 = typesArr[0] as Dictionary<string, object>;
                    if (t0 != null && t0.ContainsKey("type"))
                    {
                        var typeDict = t0["type"] as Dictionary<string, object>;
                        if (typeDict != null && typeDict.ContainsKey("name"))
                            type = Capitalize(typeDict["name"].ToString());
                    }
                }
            }

            // sprite
            string spriteUrl = "";
            if (root.ContainsKey("sprites"))
            {
                var spritesDict = root["sprites"] as Dictionary<string, object>;
                if (spritesDict != null && spritesDict.ContainsKey("front_default") && spritesDict["front_default"] != null)
                    spriteUrl = spritesDict["front_default"].ToString();
            }

            // stats
            int hp = 50, atk = 50, def = 50;
            if (root.ContainsKey("stats"))
            {
                object[] statsArr = root["stats"] as object[];
                if (statsArr != null)
                {
                    foreach (var s in statsArr)
                    {
                        var sd = s as Dictionary<string, object>;
                        if (sd == null) continue;

                        int baseStat = Convert.ToInt32(sd["base_stat"]);
                        var statDict = sd["stat"] as Dictionary<string, object>;
                        if (statDict == null) continue;

                        string statName = statDict["name"] != null ? statDict["name"].ToString() : "";
                        if (statName == "hp") hp = baseStat;
                        else if (statName == "attack") atk = baseStat;
                        else if (statName == "defense") def = baseStat;
                    }
                }
            }

            // description (species)
            string description = "";
            try
            {
                if (root.ContainsKey("species"))
                {
                    var spDict = root["species"] as Dictionary<string, object>;
                    if (spDict != null && spDict.ContainsKey("url"))
                    {
                        string spUrl = spDict["url"] != null ? spDict["url"].ToString() : "";
                        if (!string.IsNullOrWhiteSpace(spUrl))
                        {
                            string spJson = await Task.Run(() => DownloadString(spUrl));
                            var spRoot = jss.DeserializeObject(spJson) as Dictionary<string, object>;
                            if (spRoot != null && spRoot.ContainsKey("flavor_text_entries"))
                            {
                                object[] ftArr = spRoot["flavor_text_entries"] as object[];
                                if (ftArr != null)
                                {
                                    foreach (var item in ftArr)
                                    {
                                        var d = item as Dictionary<string, object>;
                                        if (d == null) continue;

                                        var langObj = d["language"] as Dictionary<string, object>;
                                        if (langObj != null && (langObj["name"] != null ? langObj["name"].ToString() : "") == "en")
                                        {
                                            description = (d["flavor_text"] != null ? d["flavor_text"].ToString() : "")
                                                .Replace("\n", " ").Replace("\f", " ").Trim();
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            Pokemon p = new Pokemon
            {
                Id = id,
                Name = name,
                Type = type,
                HP = hp,
                Attack = atk,
                Defense = def,
                ImagePath = spriteUrl,
                Description = string.IsNullOrWhiteSpace(description) ? ("Imported from PokeAPI (#" + id + ").") : description
            };

            return p;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s ?? "";
            if (s.Length == 1) return s.ToUpper();
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        // ============================================================
        // ===================== GAME 1: GUESS =========================
        // ============================================================

        private void SetupGames()
        {
            StartGuessRound();
            StartTypeQuiz();
        }

        private void StartGuessRound()
        {
            if (_pokemons.Count == 0) return;

            _guessTarget = _pokemons[_rng.Next(_pokemons.Count)];
            GuessImage.Source = LoadImage(_guessTarget.ImagePath);

            GuessMessageText.Text = "Type the name and press Check (or Enter).";
            GuessScoreText.Text = "Score: " + _guessScore + "/" + _guessTotal;
            GuessInputBox.Text = "";
        }

        private void GuessCheck_Click(object sender, RoutedEventArgs e)
        {
            CheckGuess();
        }

        private void GuessInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CheckGuess();
        }

        private void CheckGuess()
        {
            if (_guessTarget == null) return;

            string input = (GuessInputBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                GuessMessageText.Text = "Write something 🙂";
                return;
            }

            _guessTotal++;

            if (string.Equals(input, _guessTarget.Name, StringComparison.OrdinalIgnoreCase))
            {
                _guessScore++;
                GuessMessageText.Text = "✅ Correct! It's " + _guessTarget.Name + ".";
            }
            else
            {
                GuessMessageText.Text = "❌ Wrong. Correct: " + _guessTarget.Name + ".";
            }

            GuessScoreText.Text = "Score: " + _guessScore + "/" + _guessTotal;
        }

        private void GuessNext_Click(object sender, RoutedEventArgs e)
        {
            StartGuessRound();
        }

        private void GuessReveal_Click(object sender, RoutedEventArgs e)
        {
            if (_guessTarget == null) return;
            GuessMessageText.Text = "Reveal: " + _guessTarget.Name + " (" + _guessTarget.Type + ")";
        }

        // ============================================================
        // ===================== GAME 2: TYPE QUIZ =====================
        // ============================================================

        private void StartTypeQuiz()
        {
            if (_pokemons.Count == 0) return;

            _typeQuizTarget = _pokemons[_rng.Next(_pokemons.Count)];
            TypeQuizNameText.Text = _typeQuizTarget.Name;

            List<string> types = _pokemons.Select(p => p.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (types.Count < 4)
            {
                types = new List<string> { "Fire", "Water", "Grass", "Electric", "Normal", "Flying", "Poison", "Psychic" };
            }

            HashSet<string> options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            options.Add(_typeQuizTarget.Type);

            while (options.Count < 4)
            {
                options.Add(types[_rng.Next(types.Count)]);
            }

            List<string> optList = options.OrderBy(x => _rng.Next()).ToList();

            SetTypeBtn(TypeBtn1, optList[0]);
            SetTypeBtn(TypeBtn2, optList[1]);
            SetTypeBtn(TypeBtn3, optList[2]);
            SetTypeBtn(TypeBtn4, optList[3]);

            TypeQuizMessageText.Text = "Choose correct type.";
            TypeQuizScoreText.Text = "Score: " + _typeScore + "/" + _typeTotal;
        }

        private void SetTypeBtn(Button b, string type)
        {
            b.Content = type;
            b.Tag = type;
        }

        private void TypeAnswer_Click(object sender, RoutedEventArgs e)
        {
            if (_typeQuizTarget == null) return;

            Button b = sender as Button;
            string chosen = b != null && b.Tag != null ? b.Tag.ToString() : "";

            _typeTotal++;

            if (string.Equals(chosen, _typeQuizTarget.Type, StringComparison.OrdinalIgnoreCase))
            {
                _typeScore++;
                TypeQuizMessageText.Text = "✅ Correct! " + _typeQuizTarget.Name + " is " + _typeQuizTarget.Type + ".";
            }
            else
            {
                TypeQuizMessageText.Text = "❌ Wrong. " + _typeQuizTarget.Name + " is " + _typeQuizTarget.Type + ".";
            }

            TypeQuizScoreText.Text = "Score: " + _typeScore + "/" + _typeTotal;
        }

        private void TypeNext_Click(object sender, RoutedEventArgs e)
        {
            StartTypeQuiz();
        }

        // ============================================================
        // ===================== BATTLE ================================
        // ============================================================

        private void SetupBattleSelectors()
        {
            Pokemon p1Prev = P1Selector.SelectedItem as Pokemon;
            Pokemon p2Prev = P2Selector.SelectedItem as Pokemon;

            P1Selector.ItemsSource = _pokemons;
            P2Selector.ItemsSource = _pokemons;

            if (_pokemons.Count == 0) return;

            P1Selector.SelectedItem = p1Prev ?? _pokemons.FirstOrDefault();
            P2Selector.SelectedItem = p2Prev ?? _pokemons.Skip(1).FirstOrDefault() ?? _pokemons.FirstOrDefault();

            Pokemon p1 = P1Selector.SelectedItem as Pokemon;
            Pokemon p2 = P2Selector.SelectedItem as Pokemon;

            if (p1 != null) P1Image.Source = LoadImage(p1.ImagePath);
            if (p2 != null) P2Image.Source = LoadImage(p2.ImagePath);
        }

        private void P1Selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Pokemon p = P1Selector.SelectedItem as Pokemon;
            if (p != null) P1Image.Source = LoadImage(p.ImagePath);
        }

        private void P2Selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Pokemon p = P2Selector.SelectedItem as Pokemon;
            if (p != null) P2Image.Source = LoadImage(p.ImagePath);
        }

        private void StartBattle_Click(object sender, RoutedEventArgs e)
        {
            Pokemon p1Data = P1Selector.SelectedItem as Pokemon;
            Pokemon p2Data = P2Selector.SelectedItem as Pokemon;

            if (p1Data == null || p2Data == null)
            {
                MessageBox.Show("Select Fighters!");
                return;
            }

            _round = 1;
            _p1RoundsWon = 0;
            _p2RoundsWon = 0;

            StartNewRound(p1Data, p2Data);

            BattleLog.Text = "BATTLE START! (Best of 3)\nRound " + _round + ": " + p1Data.Name + " vs " + p2Data.Name;
        }

        private void StartNewRound(Pokemon p1Data, Pokemon p2Data)
        {
            double hpMult = 3.0;

            Player1 = new BattleUnit
            {
                BaseData = p1Data,
                Name = p1Data.Name,
                MaxHP = p1Data.HP * hpMult,
                CurrentHP = p1Data.HP * hpMult,
                UltPoints = 0,
                HealPotions = 3,
                UnitImage = P1Image
            };

            Player2 = new BattleUnit
            {
                BaseData = p2Data,
                Name = p2Data.Name,
                MaxHP = p2Data.HP * hpMult,
                CurrentHP = p2Data.HP * hpMult,
                UltPoints = 0,
                HealPotions = 3,
                UnitImage = P2Image
            };

            P1Image.Source = LoadImage(p1Data.ImagePath);
            P2Image.Source = LoadImage(p2Data.ImagePath);

            isBattleActive = true;
            isPlayer1Turn = true;
            BattleControls.IsEnabled = true;

            RoundInfoText.Text = "Round " + _round + " | P1: " + _p1RoundsWon + "  -  P2: " + _p2RoundsWon;
            UpdateBattleUI();
        }

        private async void Attack_Click(object sender, RoutedEventArgs e) { await PerformMove("Attack"); }
        private async void Heavy_Click(object sender, RoutedEventArgs e) { await PerformMove("Heavy"); }
        private async void Ult_Click(object sender, RoutedEventArgs e) { await PerformMove("Ult"); }

        private async void Heal_Click(object sender, RoutedEventArgs e)
        {
            BattleUnit attacker = isPlayer1Turn ? Player1 : Player2;

            if (attacker.HealPotions <= 0)
            {
                BattleLog.Text = "No potions left!";
                UpdateBattleUI();
                return;
            }

            await PerformMove("Heal");
        }

        private async Task PerformMove(string moveType)
        {
            if (!isBattleActive) return;

            BattleControls.IsEnabled = false;

            BattleUnit attacker = isPlayer1Turn ? Player1 : Player2;
            BattleUnit defender = isPlayer1Turn ? Player2 : Player1;

            double damage = 0;
            string logMsg = attacker.Name + " used " + moveType + "...";

            if (moveType != "Heal")
            {
                PlayStoryboard(isPlayer1Turn ? "AttackDashLeft" : "AttackDashRight", attacker.UnitImage);
                await Task.Delay(160);
            }

            double typeMult = GetTypeMultiplier(attacker.BaseData.Type, defender.BaseData.Type);
            bool isCrit = _rng.Next(0, 100) < 20;
            double critMult = isCrit ? 1.5 : 1.0;

            double baseDmg = (double)attacker.BaseData.Attack / Math.Max(1.0, (defender.BaseData.Defense * 0.75));

            if (moveType == "Attack")
            {
                damage = baseDmg * 12 * typeMult * critMult;
                attacker.UltPoints++;
            }
            else if (moveType == "Heavy")
            {
                if (_rng.Next(0, 100) < 70)
                {
                    damage = baseDmg * 18 * typeMult * critMult;
                    logMsg += " SMASHED!";
                    attacker.UltPoints += 2;
                }
                else
                {
                    damage = 0;
                    logMsg += " MISSED!";
                }
            }
            else if (moveType == "Heal")
            {
                double heal = attacker.MaxHP * 0.35;
                attacker.CurrentHP = Math.Min(attacker.MaxHP, attacker.CurrentHP + heal);
                attacker.HealPotions--;
                logMsg += " Healed +" + ((int)heal) + " HP. (" + attacker.HealPotions + " left)";
                PlayStoryboard("HealPulse", attacker.UnitImage);
            }
            else if (moveType == "Ult")
            {
                if (attacker.UltPoints < 3)
                {
                    BattleLog.Text = "ULT is not ready!";
                    BattleControls.IsEnabled = true;
                    UpdateBattleUI();
                    return;
                }

                damage = baseDmg * 35 * typeMult;
                logMsg += " ULTIMATE BLAST!!!";
                attacker.UltPoints = 0;
            }

            if (damage > 0)
            {
                defender.CurrentHP -= damage;

                string eff = typeMult > 1 ? " [SUPER EFF!]" : (typeMult < 1 ? " [NOT EFF]" : "");
                logMsg += " -" + ((int)damage) + " HP" + eff + (isCrit ? " CRIT!" : "");

                PlayStoryboard("DamageShake", defender.UnitImage);
                PlayStoryboard("DamageFlash", defender.UnitImage);
                await Task.Delay(220);
            }

            BattleLog.Text = logMsg;
            UpdateBattleUI();

            if (defender.CurrentHP <= 0)
            {
                if (attacker == Player1) _p1RoundsWon++;
                else _p2RoundsWon++;

                RoundInfoText.Text = "Round " + _round + " | P1: " + _p1RoundsWon + "  -  P2: " + _p2RoundsWon;
                BattleLog.Text = "✅ Round " + _round + " won by " + attacker.Name + "!";

                if (_p1RoundsWon >= 2 || _p2RoundsWon >= 2)
                {
                    string winner = _p1RoundsWon >= 2 ? Player1.Name : Player2.Name;
                    MessageBox.Show("🏆 MATCH WINNER: " + winner + "!");
                    isBattleActive = false;
                    BattleControls.IsEnabled = false;
                    return;
                }

                _round++;
                await Task.Delay(650);
                StartNewRound(Player1.BaseData, Player2.BaseData);
                BattleLog.Text = "Round " + _round + " START!";
                return;
            }

            isPlayer1Turn = !isPlayer1Turn;
            BattleControls.IsEnabled = true;
            UpdateBattleUI();
        }

        private void UpdateBattleUI()
        {
            if (!isBattleActive || Player1 == null || Player2 == null) return;

            P1NameText.Text = Player1.Name + " (" + Player1.BaseData.Type + ")";
            P2NameText.Text = Player2.Name + " (" + Player2.BaseData.Type + ")";

            P1HPBar.Maximum = Player1.MaxHP;
            P1HPBar.Value = Math.Max(0, Player1.CurrentHP);
            P1HPText.Text = ((int)Math.Max(0, Player1.CurrentHP)) + "/" + ((int)Player1.MaxHP);

            P2HPBar.Maximum = Player2.MaxHP;
            P2HPBar.Value = Math.Max(0, Player2.CurrentHP);
            P2HPText.Text = ((int)Math.Max(0, Player2.CurrentHP)) + "/" + ((int)Player2.MaxHP);

            P1Indicator.Background = isPlayer1Turn ? Brushes.LightGreen : Brushes.Transparent;
            P2Indicator.Background = !isPlayer1Turn ? Brushes.LightGreen : Brushes.Transparent;

            BattleUnit curr = isPlayer1Turn ? Player1 : Player2;

            BtnHeal.Content = "💊 HEAL (" + curr.HealPotions + ")";
            BtnHeal.IsEnabled = curr.HealPotions > 0;

            BtnUlt.Content = "✨ ULTIMATE (" + curr.UltPoints + "/3)";
            BtnUlt.IsEnabled = curr.UltPoints >= 3;
        }

        private void PlayStoryboard(string key, Image target)
        {
            try
            {
                Storyboard sb = FindResource(key) as Storyboard;
                if (sb == null || target == null) return;

                Storyboard clone = sb.Clone();
                Storyboard.SetTarget(clone, target);
                clone.Begin();
            }
            catch { }
        }

        private double GetTypeMultiplier(string attackerType, string defenderType)
        {
            string atk = (attackerType ?? "Normal").Split('/')[0].Trim();
            string def = (defenderType ?? "Normal").Split('/')[0].Trim();

            Dictionary<string, Dictionary<string, double>> chart = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Normal", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Rock",0.5 }, { "Ghost",0.0 }, { "Steel",0.5 } } },
                { "Fire",   new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",0.5 }, { "Water",0.5 }, { "Grass",2 }, { "Ice",2 }, { "Bug",2 }, { "Rock",0.5 }, { "Dragon",0.5 }, { "Steel",2 } } },
                { "Water",  new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",2 }, { "Water",0.5 }, { "Grass",0.5 }, { "Ground",2 }, { "Rock",2 }, { "Dragon",0.5 } } },
                { "Electric", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Water",2 }, { "Electric",0.5 }, { "Grass",0.5 }, { "Ground",0.0 }, { "Flying",2 }, { "Dragon",0.5 } } },
                { "Grass", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",0.5 }, { "Water",2 }, { "Grass",0.5 }, { "Poison",0.5 }, { "Ground",2 }, { "Flying",0.5 }, { "Bug",0.5 }, { "Rock",2 }, { "Dragon",0.5 }, { "Steel",0.5 } } },
                { "Flying", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Electric",0.5 }, { "Grass",2 }, { "Fighting",2 }, { "Bug",2 }, { "Rock",0.5 }, { "Steel",0.5 } } },
                { "Poison", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Grass",2 }, { "Poison",0.5 }, { "Ground",0.5 }, { "Rock",0.5 }, { "Steel",0.0 }, { "Fairy",2 } } },
                { "Psychic", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fighting",2 }, { "Poison",2 }, { "Psychic",0.5 }, { "Dark",0.0 }, { "Steel",0.5 } } },
                { "Ice", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",0.5 }, { "Water",0.5 }, { "Grass",2 }, { "Ice",0.5 }, { "Ground",2 }, { "Flying",2 }, { "Dragon",2 }, { "Steel",0.5 } } },
                { "Fighting", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Normal",2 }, { "Rock",2 }, { "Steel",2 }, { "Ice",2 }, { "Ghost",0.0 }, { "Flying",0.5 }, { "Psychic",0.5 }, { "Fairy",0.5 } } },
                { "Ground", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",2 }, { "Electric",2 }, { "Grass",0.5 }, { "Poison",2 }, { "Flying",0 }, { "Rock",2 }, { "Steel",2 } } },
                { "Bug", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Grass",2 }, { "Psychic",2 }, { "Dark",2 }, { "Fire",0.5 }, { "Flying",0.5 }, { "Fighting",0.5 } } },
                { "Rock", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fire",2 }, { "Ice",2 }, { "Flying",2 }, { "Bug",2 }, { "Fighting",0.5 }, { "Ground",0.5 }, { "Steel",0.5 } } },
                { "Dark", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Psychic",2 }, { "Ghost",2 }, { "Fighting",0.5 }, { "Dark",0.5 }, { "Fairy",0.5 } } },
                { "Fairy", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Fighting",2 }, { "Dragon",2 }, { "Dark",2 }, { "Fire",0.5 }, { "Poison",0.5 }, { "Steel",0.5 } } },
                { "Steel", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Ice",2 }, { "Rock",2 }, { "Fairy",2 }, { "Fire",0.5 }, { "Water",0.5 }, { "Electric",0.5 }, { "Steel",0.5 } } },
                { "Dragon", new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "Dragon",2 }, { "Steel",0.5 }, { "Fairy",0.0 } } },
            };

            if (chart.ContainsKey(atk) && chart[atk].ContainsKey(def)) return chart[atk][def];
            return 1.0;
        }
    }
}
