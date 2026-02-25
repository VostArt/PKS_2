using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Models;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Linq;

namespace LibraryApp
{
    public partial class GenreWindow : Window
    {
        private LibraryContext _context;
        private DataGrid _genresGrid;
        private TextBox _nameBox;
        private TextBox _descBox;

        public GenreWindow()
        {
            InitializeComponent();
            _context = new LibraryContext();
            
            // Находим элементы вручную
            _genresGrid = this.FindControl<DataGrid>("GenresGrid")!;
            _nameBox = this.FindControl<TextBox>("NameBox")!;
            _descBox = this.FindControl<TextBox>("DescBox")!;
        }

        private void Window_Opened(object sender, System.EventArgs e) => LoadGenres();

        private void LoadGenres()
        {
            if (_genresGrid != null)
                _genresGrid.ItemsSource = _context.Genres.ToList();
        }

        private async void AddGenre_Click(object sender, RoutedEventArgs e)
        {
            // 1. ОЧИСТКА ВВОДА (Убираем пробелы по краям)
            string rawName = _nameBox.Text ?? "";
            string cleanName = rawName.Trim(); 
            string desc = _descBox.Text?.Trim() ?? "";

            // 2. ПРОВЕРКА НА ПУСТОТУ
            if (string.IsNullOrEmpty(cleanName))
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Введите название жанра!").ShowAsync();
                return;
            }

            // 3. ЖЕСТКАЯ ПРОВЕРКА НА ДУБЛИКАТЫ
            // Скачиваем все названия жанров и проверяем в памяти (самый надежный способ для небольших списков)
            // Это гарантирует, что "Роман" и "роман" будут считаться одинаковыми.
            var existingGenre = _context.Genres
                .ToList() // Загружаем в память для точного сравнения
                .FirstOrDefault(g => g.Name.ToLower() == cleanName.ToLower());

            if (existingGenre != null)
            {
                await MessageBoxManager.GetMessageBoxStandard("Дубликат", 
                    $"Жанр '{existingGenre.Name}' уже существует!").ShowAsync();
                return;
            }

            // 4. СОХРАНЕНИЕ
            var newGenre = new Genre
            {
                Name = cleanName, // Сохраняем уже очищенное имя (без пробелов)
                Description = desc
            };

            _context.Genres.Add(newGenre);
            _context.SaveChanges();

            // Сброс полей
            _nameBox.Text = "";
            _descBox.Text = "";
            LoadGenres();
        }

        private async void DeleteGenre_Click(object sender, RoutedEventArgs e)
        {
            if (_genresGrid.SelectedItem is Genre selectedGenre)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Удаление", 
                    $"Удалить жанр '{selectedGenre.Name}'?", ButtonEnum.YesNo);
                
                if (await box.ShowAsync() == ButtonResult.Yes)
                {
                    _context.Genres.Remove(selectedGenre);
                    _context.SaveChanges();
                    LoadGenres();
                }
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("Инфо", "Выберите жанр для удаления").ShowAsync();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}