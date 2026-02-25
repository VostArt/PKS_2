using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Models;
using MsBox.Avalonia; 
using MsBox.Avalonia.Enums;
using System;
using System.Linq;

namespace LibraryApp
{
    public partial class AuthorWindow : Window
    {
        private LibraryContext _context;
        private DataGrid _authorsGrid;
        private TextBox _firstNameBox;
        private TextBox _lastNameBox;
        private TextBox _countryBox;

        public AuthorWindow()
        {
            InitializeComponent();
            _context = new LibraryContext();
            
            _authorsGrid = this.FindControl<DataGrid>("AuthorsGrid")!;
            _firstNameBox = this.FindControl<TextBox>("FirstNameBox")!;
            _lastNameBox = this.FindControl<TextBox>("LastNameBox")!;
            _countryBox = this.FindControl<TextBox>("CountryBox")!;
        }

        private void Window_Opened(object sender, EventArgs e) => LoadAuthors();

        private void LoadAuthors()
        {
            if (_authorsGrid != null)
                _authorsGrid.ItemsSource = _context.Authors.ToList();
        }

        private async void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            // 1. ОЧИСТКА ВВОДА
            string rawFirst = _firstNameBox.Text ?? "";
            string rawLast = _lastNameBox.Text ?? "";
            string country = _countryBox.Text?.Trim() ?? "";

            string fName = rawFirst.Trim();
            string lName = rawLast.Trim();

            // 2. ПРОВЕРКА НА ПУСТОТУ
            if (string.IsNullOrEmpty(fName) || string.IsNullOrEmpty(lName))
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Введите Имя и Фамилию!").ShowAsync();
                return;
            }

            // 3. ЖЕСТКАЯ ПРОВЕРКА НА ДУБЛИКАТЫ (Имя + Фамилия)
            // Загружаем всех авторов в память и проверяем регистронезависимо
            var existingAuthor = _context.Authors
                .ToList()
                .FirstOrDefault(a => 
                    a.FirstName.ToLower() == fName.ToLower() && 
                    a.LastName.ToLower() == lName.ToLower());

            if (existingAuthor != null)
            {
                await MessageBoxManager.GetMessageBoxStandard("Дубликат", 
                    $"Автор '{existingAuthor.FirstName} {existingAuthor.LastName}' уже существует!").ShowAsync();
                return;
            }

            // 4. СОХРАНЕНИЕ
            var author = new Author 
            { 
                FirstName = fName, 
                LastName = lName, 
                Country = country, 
                BirthDate = DateTime.Now 
            };
            
            _context.Authors.Add(author);
            _context.SaveChanges();

            _firstNameBox.Text = "";
            _lastNameBox.Text = "";
            _countryBox.Text = "";
            
            LoadAuthors();
        }

        private async void DeleteAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (_authorsGrid.SelectedItem is Author selectedAuthor)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Удаление", 
                    $"Удалить автора {selectedAuthor.LastName}?", ButtonEnum.YesNo);

                if (await box.ShowAsync() == ButtonResult.Yes)
                {
                    _context.Authors.Remove(selectedAuthor);
                    _context.SaveChanges();
                    LoadAuthors();
                }
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("Внимание", "Выберите автора в списке!").ShowAsync();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}