using Avalonia.Controls;
using Avalonia.Interactivity;
using LibraryApp.Models;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LibraryApp
{
    public partial class BookWindow : Window
    {
        private Book _book;
        private LibraryContext _context;

        // Переменные для элементов управления (чтобы не зависеть от автогенерации)
        private ListBox _authorsList;
        private ListBox _genresList;
        private TextBox _titleBox;
        private TextBox _isbnBox;
        private TextBox _yearBox;
        private TextBox _stockBox;

        // Пустой конструктор для превьюера
        public BookWindow() 
        { 
            InitializeComponent();
            _context = null!;
            _book = null!;
            
            // Инициализация заглушками
            _authorsList = null!;
            _genresList = null!;
            _titleBox = null!;
            _isbnBox = null!;
            _yearBox = null!;
            _stockBox = null!;
        }

        // Основной конструктор
        public BookWindow(Book book)
        {
            InitializeComponent();
            _context = new LibraryContext();
            _book = book;
            DataContext = _book; // Привязка данных для TextBoxes

            // 1. Находим элементы вручную через FindControl
            _authorsList = this.FindControl<ListBox>("AuthorsList")!;
            _genresList = this.FindControl<ListBox>("GenresList")!;
            _titleBox = this.FindControl<TextBox>("TitleBox")!;
            _isbnBox = this.FindControl<TextBox>("IsbnBox")!;
            _yearBox = this.FindControl<TextBox>("YearBox")!;
            _stockBox = this.FindControl<TextBox>("StockBox")!;

            // 2. Загружаем списки авторов и жанров
            LoadLists();
        }

        private void LoadLists()
        {
            if (_authorsList == null || _genresList == null) return;

            // --- АВТОРЫ ---
            var allAuthors = _context.Authors.ToList();
            _authorsList.ItemsSource = allAuthors;

            // Выделяем тех авторов, которые уже есть у книги
            var selectedAuthors = new List<Author>();
            foreach (var author in allAuthors)
            {
                // Проверяем по ID, так как объекты могут быть из разных контекстов
                if (_book.Authors.Any(a => a.Id == author.Id))
                {
                    selectedAuthors.Add(author);
                }
            }
            _authorsList.SelectedItems = selectedAuthors;

            // --- ЖАНРЫ ---
            var allGenres = _context.Genres.ToList();
            _genresList.ItemsSource = allGenres;

            var selectedGenres = new List<Genre>();
            foreach (var genre in allGenres)
            {
                if (_book.Genres.Any(g => g.Id == genre.Id))
                {
                    selectedGenres.Add(genre);
                }
            }
            _genresList.SelectedItems = selectedGenres;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // === 1. ВАЛИДАЦИЯ ===

            // Проверка названия
            if (string.IsNullOrWhiteSpace(_titleBox.Text))
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Введите название книги!").ShowAsync();
                return;
            }

            // Проверка ISBN
            string isbn = _isbnBox.Text?.Trim() ?? "";
            // Регулярное выражение: ^ - начало, \d+ - только цифры, $ - конец строки.
            if (!Regex.IsMatch(isbn, @"^\d+$") || (isbn.Length != 10 && isbn.Length != 13))
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "ISBN должен состоять только из цифр и быть длиной 10 или 13 символов!").ShowAsync();
                return;
            }
            // Обновляем поле в объекте (на всякий случай, если Binding не сработал мгновенно)
            _book.ISBN = isbn;
            _book.Title = _titleBox.Text; // Тоже обновляем явно

            // Проверка на ДУБЛИКАТЫ в БД
            // Ищем книгу с таким же ISBN или Названием, но исключаем текущую книгу (по ID)
            bool isDuplicate = _context.Books.Any(b => 
                (b.ISBN == isbn || b.Title.ToLower() == _book.Title.ToLower()) && 
                b.Id != _book.Id);

            if (isDuplicate)
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Книга с таким названием или ISBN уже существует!").ShowAsync();
                return;
            }

            // Проверка выбора списков
            if (_authorsList.SelectedItems == null || _authorsList.SelectedItems.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Выберите хотя бы одного автора!").ShowAsync();
                return;
            }
            if (_genresList.SelectedItems == null || _genresList.SelectedItems.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxStandard("Ошибка", "Выберите хотя бы один жанр!").ShowAsync();
                return;
            }

            // === 2. СОХРАНЕНИЕ СВЯЗЕЙ ===

            // Очищаем текущие списки в объекте книги
            _book.Authors.Clear();
            _book.Genres.Clear();

            // Добавляем выбранных авторов
            foreach (var item in _authorsList.SelectedItems)
            {
                if (item is Author a)
                {
                    // Важный момент: мы берем авторов из контекста этого окна (_context).
                    // Это безопасно для сохранения.
                    _book.Authors.Add(a);
                }
            }

            // Добавляем выбранные жанры
            foreach (var item in _genresList.SelectedItems)
            {
                if (item is Genre g)
                {
                    _book.Genres.Add(g);
                }
            }

            // Возвращаем true (успех) и закрываем окно.
            // Само сохранение в БД (_context.SaveChanges) происходит в MainWindow.
            Close(true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}