using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


class Program
{
    static string CsvPath;
    static List<MovieRecord> Movies;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <path_to_tmdb_5000_credits.csv>");
            return;
        }
        CsvPath = args[0];
        Console.WriteLine("Загрузка данных из: " + CsvPath);
        Movies = LoadCsv(CsvPath);
        Console.WriteLine($"Загружено фильмов: {Movies.Count}");

        // Предварительные индексы
        var peopleIndex = BuildPeopleIndex(Movies);
        var deptIndex = BuildDepartmentIndex(Movies);

        while (true)
        {
            Console.WriteLine("\nВыберите запрос (введите номер) или 0 для выхода:");
            Console.WriteLine("1. Все фильмы режиссера 'Steven Spielberg'");
            Console.WriteLine("2. Список персонажей, которых сыграл 'Tom Hanks'");
            Console.WriteLine("3. 5 фильмов с самым большим количеством актеров");
            Console.WriteLine("4. Топ-10 самых востребованных актеров (по количеству фильмов)");
            Console.WriteLine("5. Список всех уникальных департаментов (department)");
            Console.WriteLine("6. Все фильмы, где 'Hans Zimmer' был композитором (Original Music Composer)");
            Console.WriteLine("7. Словарь: ключ = movie_id, значение = имя режиссера");
            Console.WriteLine("8. Фильмы с Brad Pitt и George Clooney в составе");
            Console.WriteLine("9. Сколько всего человек работает в департаменте 'Camera' по всем фильмам");
            Console.WriteLine("10. Люди, которые в 'Titanic' были и в съемочной группе, и в списке актеров");
            Console.WriteLine("11. Внутренний круг Quentin Tarantino (топ-5 членов съёмочной группы)");
            Console.WriteLine("12. Топ-10 пар актёров, которые чаще всего снимались вместе");
            Console.WriteLine("13. Индекс разнообразия: 5 членов съемочной группы, работавших в наибольшем числе департаментов");
            Console.WriteLine("14. Творческие трио: фильмы, где один человек был Director, Writer и Producer");
            Console.WriteLine("15. Два шага до Kevin Bacon (1-ступень и 2-ступень)");
            Console.WriteLine("16. Проанализировать командную работу: сгруппировать по режиссеру и средние размеры Cast/Crew");
            Console.WriteLine("17. Карьерный путь универсалов: для каждого человека, кто был и актером, и членом съемочной группы, департамент где чаще всего работал");
            Console.WriteLine("18. Пересечение: люди, работавшие и с Martin Scorsese, и с Christopher Nolan");
            Console.WriteLine("19. Ранжировать департаменты по среднему количеству актёров в фильмах, над которыми они работали");
            Console.WriteLine("20. Архетипы персонажей Johnny Depp (группировка по первому слову роли)");

            Console.Write("Номер: ");
            var line = Console.ReadLine();
            if (!int.TryParse(line, out int choice)) break;
            if (choice == 0) break;

            switch (choice)
            {
                case 1: Query_FilmsByDirector("Steven Spielberg"); break;
                case 2: Query_CharactersByActor("Tom Hanks"); break;
                case 3: Query_Top5ByCastSize(); break;
                case 4: Query_Top10ActorsByFilmCount(); break;
                case 5: Query_UniqueDepartments(); break;
                case 6: Query_FilmsByComposer("Hans Zimmer"); break;
                case 7: Query_MovieIdToDirectorDict(); break;
                case 8: Query_FilmsWithActors(new[] { "Brad Pitt", "George Clooney" }); break;
                case 9: Query_CountPeopleInDepartment("Camera"); break;
                case 10: Query_PeopleInBothCastAndCrew("Titanic"); break;
                case 11: Query_DirectorInnerCircle("Quentin Tarantino", 5); break;
                case 12: Query_TopActorPairs(10); break;
                case 13: Query_TopDiversityIndexes(5); break;
                case 14: Query_CreativeTrios(); break;
                case 15: Query_TwoStepsToKevinBacon("Kevin Bacon"); break;
                case 16: Query_GroupByDirectorAverageSizes(); break;
                case 17: Query_UniversalCareerPaths(); break;
                case 18: Query_IntersectionTwoDirectors("Martin Scorsese", "Christopher Nolan"); break;
                case 19: Query_DepartmentsByAvgCastSize(); break;
                case 20: Query_JohnnyDeppArchetypes("Johnny Depp"); break;
                default: Console.WriteLine("Неверный выбор"); break;
            }
        }
    }

    // -------------------- Загрузка и модели --------------------
    class MovieRecord
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public List<CastPerson> Cast { get; set; } = new();
        public List<CrewPerson> Crew { get; set; } = new();
    }
    class CastPerson { public int Id; public string Name; public string Character; }
    class CrewPerson { public int Id; public string Name; public string Department; public string Job; }

    static List<MovieRecord> LoadCsv(string path)
    {
        var list = new List<MovieRecord>();
        using var sr = new StreamReader(path);
        string header = sr.ReadLine();
        // ожидаем: movie_id,title,cast,crew (порядок может варьироваться). Попробуем корректно распарсить заголовок.
        var headers = SplitCsvLine(header);
        int idx_movie_id = Array.IndexOf(headers, "movie_id");
        int idx_title = Array.IndexOf(headers, "title");
        int idx_cast = Array.IndexOf(headers, "cast");
        int idx_crew = Array.IndexOf(headers, "crew");
        if (idx_movie_id < 0 || idx_title < 0 || idx_cast < 0 || idx_crew < 0)
            throw new Exception("CSV не содержит необходимых столбцов (movie_id,title,cast,crew)");

        string line;
        while ((line = sr.ReadLine()) != null)
        {
            var cols = SplitCsvLine(line);
            try
            {
                var mr = new MovieRecord();
                mr.MovieId = int.Parse(cols[idx_movie_id]);
                mr.Title = cols[idx_title];
                // parse JSON fields
                var castJson = cols[idx_cast];
                var crewJson = cols[idx_crew];
                mr.Cast = ParseCast(castJson);
                mr.Crew = ParseCrew(crewJson);
                list.Add(mr);
            }
            catch (Exception ex)
            {
                // пропускаем проблемные строки, но логируем
                Console.WriteLine("Ошибка при разборе строки: " + ex.Message);
            }
        }
        return list;
    }

    // Очень простой CSV-сплиттер, корректно обрабатывает поля в кавычках и запятые внутри
    static string[] SplitCsvLine(string line)
    {
        var parts = new List<string>();
        bool inQuotes = false;
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                // двойные кавычки внутри поля представлены как ""
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cur.Append('"'); i++; // пропустить второй
                }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(cur.ToString()); cur.Clear();
            }
            else cur.Append(c);
        }
        parts.Add(cur.ToString());
        return parts.ToArray();
    }

    static List<CastPerson> ParseCast(string json)
    {
        try
        {
            var arr = JArray.Parse(json);
            var res = new List<CastPerson>();
            foreach (var it in arr)
            {
                res.Add(new CastPerson
                {
                    Id = it.Value<int?>("id") ?? 0,
                    Name = it.Value<string>("name") ?? "",
                    Character = it.Value<string>("character") ?? ""
                });
            }
            return res;
        }
        catch
        {
            return new List<CastPerson>();
        }
    }
    static List<CrewPerson> ParseCrew(string json)
    {
        try
        {            var arr = JArray.Parse(json);
            var res = new List<CrewPerson>();
            foreach (var it in arr)
            {
                res.Add(new CrewPerson
                {
                    Id = it.Value<int?>("id") ?? 0,
                    Name = it.Value<string>("name") ?? "",
                    Department = it.Value<string>("department") ?? "",
                    Job = it.Value<string>("job") ?? ""
                });
            }
            return res;
        }
        catch
        {
            return new List<CrewPerson>();
        }
    }

    // -------------------- Индексы --------------------
    static Dictionary<string, List<MovieRecord>> BuildPeopleIndex(List<MovieRecord> movies)
    {
        var index = new Dictionary<string, List<MovieRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in movies)
        {
            foreach (var c in m.Cast)
            {
                if (!index.TryGetValue(c.Name, out var list)) { list = new(); index[c.Name] = list; }
                list.Add(m);
            }
            foreach (var c in m.Crew)
            {
                if (!index.TryGetValue(c.Name, out var list)) { list = new(); index[c.Name] = list; }
                list.Add(m);
            }
        }
        return index;
    }
    static Dictionary<string, List<(MovieRecord movie, CrewPerson person)>> BuildDepartmentIndex(List<MovieRecord> movies)
    {
        var dict = new Dictionary<string, List<(MovieRecord, CrewPerson)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in movies)
        {
            foreach (var c in m.Crew)
            {
                if (!dict.TryGetValue(c.Department, out var list)) { list = new(); dict[c.Department] = list; }
                list.Add((m, c));
            }
        }
        return dict;
    }

    // -------------------- Запросы --------------------
    static void Query_FilmsByDirector(string directorName)
    {
        var res = Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(directorName, StringComparison.OrdinalIgnoreCase))).ToList();
        Console.WriteLine($"Найдено фильмов у режиссера '{directorName}': {res.Count}");
        foreach (var r in res) Console.WriteLine($"{r.MovieId}\t{r.Title}");
    }

    static void Query_CharactersByActor(string actorName)
    {
        var chars = Movies.SelectMany(m => m.Cast.Where(c => c.Name.Equals(actorName, StringComparison.OrdinalIgnoreCase)).Select(c => c.Character)).Distinct().ToList();
        Console.WriteLine($"Персонажей у '{actorName}': {chars.Count}");
        foreach (var ch in chars) Console.WriteLine(ch);
    }

    static void Query_Top5ByCastSize()
    {
        var top = Movies.OrderByDescending(m => m.Cast.Count).Take(5).ToList();
        Console.WriteLine("Топ-5 фильмов по количеству актёров:");
        foreach (var m in top) Console.WriteLine($"{m.Title} (id={m.MovieId}) — cast={m.Cast.Count}");
    }

    static void Query_Top10ActorsByFilmCount()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Movies)
            foreach (var c in m.Cast) counts[c.Name] = counts.GetValueOrDefault(c.Name) + 1;
        foreach (var kv in counts.OrderByDescending(kv => kv.Value).Take(10)) Console.WriteLine($"{kv.Key} — {kv.Value}");
    }

    static void Query_UniqueDepartments()
    {
        var deps = Movies.SelectMany(m => m.Crew.Select(c => c.Department)).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        Console.WriteLine("Уникальные департаменты:");
        foreach (var d in deps) Console.WriteLine(d);
    }

    static void Query_FilmsByComposer(string composerName)
    {
        var res = Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Original Music Composer", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(composerName, StringComparison.OrdinalIgnoreCase))).ToList();
        Console.WriteLine($"Фильмы, где '{composerName}' — Original Music Composer: {res.Count}");
        foreach (var r in res) Console.WriteLine(r.Title);
    }

    static void Query_MovieIdToDirectorDict()
    {
        var dict = new Dictionary<int, string>();
        foreach (var m in Movies)
        {
            var dir = m.Crew.FirstOrDefault(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase));
            dict[m.MovieId] = dir?.Name ?? "";
        }
        Console.WriteLine("Словарь (movie_id -> director):");
        foreach (var kv in dict) Console.WriteLine($"{kv.Key} => {kv.Value}");
    }

    static void Query_FilmsWithActors(string[] actorNames)
    {
        var res = Movies.Where(m => actorNames.All(a => m.Cast.Any(c => c.Name.Equals(a, StringComparison.OrdinalIgnoreCase)))).ToList();
        Console.WriteLine($"Фильмов с {string.Join(" и ", actorNames)}: {res.Count}");
        foreach (var r in res) Console.WriteLine(r.Title);
    }

    static void Query_CountPeopleInDepartment(string department)
    {
        var people = Movies.SelectMany(m => m.Crew.Where(c => c.Department.Equals(department, StringComparison.OrdinalIgnoreCase)).Select(c => c.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Console.WriteLine($"Всего уникальных человек в департаменте '{department}': {people}");
    }

    static void Query_PeopleInBothCastAndCrew(string movieTitle)
    {
        var movie = Movies.FirstOrDefault(m => m.Title.Equals(movieTitle, StringComparison.OrdinalIgnoreCase));
        if (movie == null) { Console.WriteLine("Фильм не найден"); return; }
        var castNames = movie.Cast.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var crewNames = movie.Crew.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var both = castNames.Intersect(crewNames, StringComparer.OrdinalIgnoreCase).ToList();
        Console.WriteLine($"В фильме '{movieTitle}' одновременно в cast и crew: {both.Count}");
        foreach (var b in both) Console.WriteLine(b);
    }

    static void Query_DirectorInnerCircle(string directorName, int topN)
    {
        // для фильмов режиссера собрать всех членов crew (не актёров) и посчитать частоту
        var films = Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(directorName, StringComparison.OrdinalIgnoreCase))).ToList();
        var counter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in films)
            foreach (var c in f.Crew.Where(c => !c.Job.Equals("Actor", StringComparison.OrdinalIgnoreCase)))
                if (!c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase)) counter[c.Name] = counter.GetValueOrDefault(c.Name) + 1;
        foreach (var kv in counter.OrderByDescending(kv => kv.Value).Take(topN)) Console.WriteLine($"{kv.Key} — совместных фильмов: {kv.Value}");
    }

    static void Query_TopActorPairs(int topN)
    {
        var pairCounts = new Dictionary<(string, string), int>();
        foreach (var m in Movies)
        {
            var names = m.Cast.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToArray();
            for (int i = 0; i < names.Length; i++)
                for (int j = i + 1; j < names.Length; j++)
                {
                    var pair = (names[i], names[j]);
                    pairCounts[pair] = pairCounts.GetValueOrDefault(pair) + 1;
                }
        }
        foreach (var kv in pairCounts.OrderByDescending(kv => kv.Value).Take(topN)) Console.WriteLine($"{kv.Key.Item1} / {kv.Key.Item2} — совместных фильмов: {kv.Value}");
    }

    static void Query_TopDiversityIndexes(int topN)
    {
        var personDepts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Movies)
            foreach (var c in m.Crew)
            {
                if (!personDepts.TryGetValue(c.Name, out var set)) { set = new(); personDepts[c.Name] = set; }
                if (!string.IsNullOrEmpty(c.Department)) set.Add(c.Department);
            }
        foreach (var kv in personDepts.OrderByDescending(kv => kv.Value.Count).Take(topN)) Console.WriteLine($"{kv.Key} — разных департаментов: {kv.Value.Count}");
    }

    static void Query_CreativeTrios()
    {
        var res = new List<(MovieRecord, string)>();
        foreach (var m in Movies)
        {
            // найти людей, у которых в crew есть Job Director, Writer, Producer — но должности в разных элементах массива; нам нужно имя, у которого есть все три роли
            var groups = m.Crew.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var g in groups)
            {
                var jobs = g.Select(x => x.Job).Where(j => !string.IsNullOrEmpty(j)).Select(j => j.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (jobs.Contains("Director") && (jobs.Contains("Writer") || jobs.Contains("Screenplay") || jobs.Contains("Author")) && jobs.Contains("Producer"))
                {
                    res.Add((m, g.Key));
                }
            }
        }
        Console.WriteLine($"Найдено творческих трио: {res.Count}");
        foreach (var r in res) Console.WriteLine($"{r.Item1.Title} — {r.Item2}");
    }

    static void Query_TwoStepsToKevinBacon(string kevin)
    {
        // шаг 1: актеры, которые снимались с Kevin Bacon (в одном фильме)
        var filmsWithKevin = Movies.Where(m => m.Cast.Any(c => c.Name.Equals(kevin, StringComparison.OrdinalIgnoreCase))).ToList();
        var step1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in filmsWithKevin) foreach (var c in m.Cast) if (!c.Name.Equals(kevin, StringComparison.OrdinalIgnoreCase)) step1.Add(c.Name);
        Console.WriteLine($"Актёры 1-ступени до {kevin}: {step1.Count}");
        // шаг 2: актеры, которые снимались с любым из step1 (в одном фильме)
        var step2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Movies)
        {
            var names = m.Cast.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (names.Overlaps(step1)) foreach (var n in names) if (!step1.Contains(n) && !n.Equals(kevin, StringComparison.OrdinalIgnoreCase)) step2.Add(n);
        }
        Console.WriteLine($"Актёры 2-ступени до {kevin}: {step2.Count}");
        Console.WriteLine("Первые 100 из двух ступеней:\n");
        foreach (var n in step1.Take(50)) Console.WriteLine("1-ступень: " + n);
        foreach (var n in step2.Take(50)) Console.WriteLine("2-ступень: " + n);
    }

    static void Query_GroupByDirectorAverageSizes()
    {
        var byDirector = new Dictionary<string, List<MovieRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Movies)
        {
            var dirs = m.Crew.Where(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name).DefaultIfEmpty("(unknown)");
            foreach (var d in dirs)
            {
                if (!byDirector.TryGetValue(d, out var list)) { list = new(); byDirector[d] = list; }
                byDirector[d].Add(m);
            }
        }
        Console.WriteLine("Director \t avgCast \t avgCrew \t filmsCount");
        foreach (var kv in byDirector.OrderByDescending(kv => kv.Value.Count).Take(200))
        {
            var avgCast = kv.Value.Average(m => m.Cast.Count);
            var avgCrew = kv.Value.Average(m => m.Crew.Count);
            Console.WriteLine($"{kv.Key}\t{avgCast:F2}\t{avgCrew:F2}\t{kv.Value.Count}");
        }
    }

    static void Query_UniversalCareerPaths()
    {
        // для каждого человека, который был и актёром, и членом съемочной группы, найти департамент, в котором он работал чаще всего
        var actorSet = new HashSet<string>(Movies.SelectMany(m => m.Cast.Select(c => c.Name)), StringComparer.OrdinalIgnoreCase);
        var crewPeople = Movies.SelectMany(m => m.Crew).GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        Console.WriteLine("Человек -> наиболее частый департамент (только для тех, кто был и актёром, и в crew)");
        foreach (var name in actorSet)
        {
            if (crewPeople.TryGetValue(name, out var entries))
            {
                var topDept = entries.GroupBy(e => e.Department, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "(unknown)";
                Console.WriteLine($"{name} => {topDept}");
            }
        }
    }

    static void Query_IntersectionTwoDirectors(string dir1, string dir2)
    {
        var people1 = Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(dir1, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(m => m.Crew.Select(c => c.Name)).Concat(Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(dir1, StringComparison.OrdinalIgnoreCase))).SelectMany(m=>m.Cast.Select(c=>c.Name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var people2 = Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(dir2, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(m => m.Crew.Select(c => c.Name)).Concat(Movies.Where(m => m.Crew.Any(c => c.Job.Equals("Director", StringComparison.OrdinalIgnoreCase) && c.Name.Equals(dir2, StringComparison.OrdinalIgnoreCase))).SelectMany(m=>m.Cast.Select(c=>c.Name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inter = people1.Intersect(people2, StringComparer.OrdinalIgnoreCase).ToList();
        Console.WriteLine($"Люди, работавшие и с {dir1}, и с {dir2}: {inter.Count}");
        foreach (var p in inter) Console.WriteLine(p);
    }

    static void Query_DepartmentsByAvgCastSize()
    {
        // для каждого департамента вычислим среднее количество актёров в фильмах, где есть хотя бы один человек из этого департамента
        var deptToMovies = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in Movies)
            foreach (var c in m.Crew)
            {
                if (string.IsNullOrEmpty(c.Department)) continue;
                if (!deptToMovies.TryGetValue(c.Department, out var set)) { set = new HashSet<int>(); deptToMovies[c.Department] = set; }
                set.Add(m.MovieId);
            }
        var res = new List<(string dept, double avgCast)>();
        foreach (var kv in deptToMovies)
        {
            var movies = Movies.Where(m => kv.Value.Contains(m.MovieId)).ToList();
            var avg = movies.Average(m => m.Cast.Count);
            res.Add((kv.Key, avg));
        }
        foreach (var r in res.OrderByDescending(x => x.avgCast)) Console.WriteLine($"{r.dept} — avg cast size = {r.avgCast:F2}");
    }

    static void Query_JohnnyDeppArchetypes(string actorName)
    {
        var roles = Movies.SelectMany(m => m.Cast.Where(c => c.Name.Equals(actorName, StringComparison.OrdinalIgnoreCase)).Select(c => c.Character)).Where(s=>!string.IsNullOrEmpty(s)).ToList();
        var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roles)
        {
            var first = r.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            groups[first] = groups.GetValueOrDefault(first) + 1;
        }
        Console.WriteLine($"Архетипы персонажей для {actorName}:");
        foreach (var kv in groups.OrderByDescending(kv => kv.Value)) Console.WriteLine($"{kv.Key} — {kv.Value}");
    }
}

// extension method
static class Ext
{
    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key) where TKey : notnull
    {
        if (d.TryGetValue(key, out var v)) return v;
        return default(TValue);
    }
}
