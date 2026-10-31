using LanguageGenerator;

var lang = new Language();

var rand = new Random();
if (rand.NextDouble() < 0.7)
{
    lang.HasArticles = true;
}
