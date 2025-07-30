# Przewodnik Migracji do .NET MAUI

Witaj! Ten przewodnik pomoże Ci, krok po kroku, stworzyć nową aplikację w technologii .NET MAUI, która będzie działać na systemach Windows i macOS. Wykorzystamy w niej bibliotekę `BdlGusExporter.Core`, którą przygotowałem. Biblioteka ta zawiera całą logikę biznesową aplikacji.

Twoim zadaniem będzie zbudowanie interfejsu użytkownika (UI) i połączenie go z tą biblioteką.

## Krok 1: Przygotowanie środowiska

Będziesz potrzebować komputera z systemem **Windows** lub **macOS**.

1.  **Zainstaluj .NET 8 SDK:** Pobierz i zainstaluj .NET 8 ze strony Microsoft: [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
2.  **Zainstaluj .NET MAUI:** Otwórz terminal (wiersz polecenia) i wpisz komendę:
    ```bash
    dotnet workload install maui
    ```
3.  **Dodatkowe narzędzia:**
    *   **Na Windows:** Zainstaluj Visual Studio 2022 z komponentem ".NET Multi-platform App UI development".
    *   **Na macOS:** Zainstaluj Visual Studio for Mac lub Visual Studio Code z rozszerzeniem .NET MAUI. Będziesz również potrzebować Xcode z App Store.

## Krok 2: Stworzenie nowego projektu .NET MAUI

1.  W terminalu, w głównym folderze tego repozytorium, stwórz nowy projekt MAUI. Nazwijmy go `BdlGusExporter.Maui`.
    ```bash
    dotnet new maui -n BdlGusExporter.Maui
    ```
2.  Dodaj nowo stworzony projekt do solucji (`gus.sln`), aby łatwiej zarządzać zależnościami.
    ```bash
    dotnet sln BdlGusExporter/gus.sln add BdlGusExporter.Maui/BdlGusExporter.Maui.csproj
    ```
3.  Dodaj referencję z projektu MAUI do naszej biblioteki `Core`.
    ```bash
    dotnet add BdlGusExporter.Maui/BdlGusExporter.Maui.csproj reference BdlGusExporter.Core/BdlGusExporter.Core.csproj
    ```

## Krok 3: Budowa Interfejsu Użytkownika (UI)

Teraz najciekawsza część. Musisz odtworzyć interfejs użytkownika w pliku `BdlGusExporter.Maui/MainPage.xaml`. Poniżej znajduje się prosty przykład, jak możesz zacząć. Zastąp całą zawartość `MainPage.xaml` tym kodem:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="BdlGusExporter.Maui.MainPage"
             Title="BDL GUS Exporter (MAUI)">
    <VerticalStackLayout Spacing="10" Padding="20">

        <Label Text="BDL GUS Exporter" FontSize="Large" FontAttributes="Bold" HorizontalOptions="Center" />

        <!-- Sekcja klucza API -->
        <HorizontalStackLayout Spacing="10">
            <CheckBox x:Name="chkUseApiKey" />
            <Label Text="Użyj klucza API" VerticalOptions="Center" />
            <Entry x:Name="txtApiKey" Placeholder="Wpisz swój klucz API" WidthRequest="300" />
        </HorizontalStackLayout>

        <!-- Tutaj umieścisz resztę kontrolek:
             - Drzewo lub listę na jednostki (możesz zacząć od `ListView` lub `CollectionView`)
             - Przyciski "Dodaj", "Usuń", "Wyczyść", "Eksportuj"
             - Listę wybranych jednostek
             - Pasek postępu i status
        -->

        <Button Text="Eksportuj (do zaimplementowania)" />
        <Label x:Name="lblStatus" Text="Gotowy" />

    </VerticalStackLayout>
</ContentPage>
```

## Krok 4: Połączenie UI z logiką (Code-behind)

Teraz musisz ożywić interfejs. Otwórz plik `BdlGusExporter.Maui/MainPage.xaml.cs`. Będziesz w nim używać `GusApiService` i `ExcelExportService`.

Oto uproszczony szkielet kodu, od którego możesz zacząć:

```csharp
using BdlGusExporter.Core;

namespace BdlGusExporter.Maui;

public partial class MainPage : ContentPage
{
    private readonly GusApiService _gusApiService = new();
    private readonly ExcelExportService _excelExportService = new();

    public MainPage()
    {
        InitializeComponent();
        // Tutaj będziesz pisać logikę, np. ładowanie jednostek po starcie
        // LoadUnits();
    }

    // Przykładowa metoda, którą musisz zaimplementować
    private async void OnExportClicked(object sender, EventArgs e)
    {
        // 1. Zbierz wybrane jednostki z UI
        // 2. Wczytaj `variables.txt`
        // 3. Użyj _gusApiService do pobrania danych w pętli
        // 4. Przygotuj dane w formacie `Dictionary<string, List<ExportDataRow>>`
        // 5. Użyj `_excelExportService.CreateWorkbook(dane)`
        // 6. Zapisz plik - w MAUI będziesz potrzebować np. CommunityToolkit.Maui.Storage

        await DisplayAlert("Informacja", "Logika eksportu nie jest jeszcze zaimplementowana!", "OK");
    }
}
```

## Krok 5: Zapisywanie pliku w MAUI

Zapisywanie plików w MAUI działa inaczej niż w WPF. Nie ma `SaveFileDialog`. Zamiast tego, polecam użyć biblioteki `CommunityToolkit.Maui`.

1.  Dodaj pakiet do projektu MAUI:
    ```bash
    dotnet add BdlGusExporter.Maui/BdlGusExporter.Maui.csproj package CommunityToolkit.Maui
    ```
2.  Zarejestruj bibliotekę w `MauiProgram.cs` (dodaj `.UseMauiCommunityToolkit()`).
3.  Użyj `FileSaver` z tej biblioteki, aby zapisać wygenerowany plik Excel.

## Podsumowanie

Twoim zadaniem jest rozbudowanie interfejsu w `MainPage.xaml` i uzupełnienie logiki w `MainPage.xaml.cs`, korzystając z gotowych serwisów w `BdlGusExporter.Core`. Powodzenia!
