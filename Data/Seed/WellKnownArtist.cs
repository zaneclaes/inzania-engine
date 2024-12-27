#region

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IZ.Core.Contexts;
using IZ.Core.Data;
using IZ.Core.Exceptions;
using Tuneality.Core.Songs;
using Tuneality.Core.Songs.Scores;

#endregion

namespace IZ.Data.Seed;

public class WellKnownArtist {

  public WellKnownArtist(string name, params string[] otherNames) {
    Name = name;
    Aliases = new List<string> {
      name
    }.Union(otherNames).ToList();
    List<string>? patterns = Aliases.Select(n => $"(.*{n}.*)").ToList();
    Matcher = new Regex($"^{string.Join("|", patterns)}$", RegexOptions.IgnoreCase);
  }
  public Regex Matcher { get; }

  public string Name { get; }

  public List<string> Aliases { get; }

  [NotMapped]
  public List<string> AllNames => new[] {
    Name
  }.Union(Aliases).ToList();

  public static List<WellKnownArtist> List { get; } = new List<WellKnownArtist> {
    new WellKnownArtist("Johann Sebastian Bach", "Bach"),
    new WellKnownArtist("Wolfgang Amadeus Mozart", "Mozart"),
    new WellKnownArtist("Claude Debussy", "Debussy"),
    new WellKnownArtist("Frédéric Chopin", "Chopin"),
    new WellKnownArtist("Ludwig van Beethoven", "Beethoven"),
    new WellKnownArtist("Johann Pachelbel", "Pachelbel"),
    new WellKnownArtist("Scott Joplin"),
    new WellKnownArtist("Pyotr Ilyich Tchaikovsky", "Tchaikovsky"),
    new WellKnownArtist("Franz Liszt", "Liszt"),
    new WellKnownArtist("Franz Schubert", "Schubert"),
    new WellKnownArtist("Antonio Vivaldi", "Vivaldi"),
    new WellKnownArtist("Franz Gruber", "Gruber"),
    new WellKnownArtist("Erik Satie", "Satie"),
    new WellKnownArtist("Johann Strauss II", "Strauss"),
    new WellKnownArtist("The Beatles", "Beatles"),
    new WellKnownArtist("Taylor Swift"),
    new WellKnownArtist("Ed Sheeran", "Sheeran"),
    new WellKnownArtist("Daft Punk"),
    new WellKnownArtist("John Legend"),
    new WellKnownArtist("Adele"),
    new WellKnownArtist("Alicia Keys"),
    new WellKnownArtist("Coldplay"),
    new WellKnownArtist("Elvis Presley", "Elvis"),
    new WellKnownArtist("Bruno Mars"),
    new WellKnownArtist("Billy Joel"),
    new WellKnownArtist("Elton John"),
    new WellKnownArtist("Stevie Wonder"),
    new WellKnownArtist("Lady Gaga"),
    new WellKnownArtist("Ray Charles"),
    new WellKnownArtist("Little Richard"),
    new WellKnownArtist("Barry Manilow"),
    new WellKnownArtist("Bruce Springsteen", "Springsteen"),
    new WellKnownArtist("Sara Bareilles"),
    new WellKnownArtist("The Eagles"),
    new WellKnownArtist("Philip Glass"),
    new WellKnownArtist("Queen"),
    new WellKnownArtist("Supremes"),
    new WellKnownArtist("Simon and Garfunkel"),
    new WellKnownArtist("Bob Dylan"),
    new WellKnownArtist("Sly and the Family Stone"),
    new WellKnownArtist("ABBA"),
    new WellKnownArtist("Don Henley"),
    new WellKnownArtist("Imagine Dragons"),
    new WellKnownArtist("Ben E. King"),
    new WellKnownArtist("Alan Walker"),
    new WellKnownArtist("Swedish House Mafia"),
    new WellKnownArtist("Bee Gees"),
    new WellKnownArtist("Metallica"),
    new WellKnownArtist("R. Kelly"),
    new WellKnownArtist("Madonna"),
    new WellKnownArtist("AC/DC", "ACDC"),
    new WellKnownArtist("The Kinks"),
    new WellKnownArtist("Kanye West"),
    new WellKnownArtist("Van Morrison"),
    new WellKnownArtist("The Band"),
    new WellKnownArtist("Guns n' Roses"),
    new WellKnownArtist("Pink Floyd"),
    new WellKnownArtist("The Rolling Stones"),
    new WellKnownArtist("Led Zeppelin"),
    new WellKnownArtist("Nirvana"),
    new WellKnownArtist("The Who"),
    new WellKnownArtist("U2"),
    new WellKnownArtist("Grateful Dead"),
    new WellKnownArtist("Journey"),
    new WellKnownArtist("The Police"),
    new WellKnownArtist("The Beach Boys"),
    new WellKnownArtist("The Doors"),
    new WellKnownArtist("Pearl Jam"),
    new WellKnownArtist("Radiohead"),
    new WellKnownArtist("Rush"),
    new WellKnownArtist("Fleetwood Mac"),
    new WellKnownArtist("Aerosmith"),
    new WellKnownArtist("Black Sabbath"),
    new WellKnownArtist("Red Hot Chili Peppers"),
    new WellKnownArtist("The Kinks"),
    new WellKnownArtist("Green Day"),
    new WellKnownArtist("Linkin Park"),
    new WellKnownArtist("Oasis"),
    new WellKnownArtist("Creedence Clearwater Revival"),
    new WellKnownArtist("Tom Petty and the Heartbreakers"),
    new WellKnownArtist("Def Leppard"),
    new WellKnownArtist("ZZ Top"),
    new WellKnownArtist("Genesis"),
    new WellKnownArtist("The Clash"),
    new WellKnownArtist("Kiss"),
    new WellKnownArtist("Maroon 5"),
    new WellKnownArtist("Iron Maiden"),
    new WellKnownArtist("Foo Fighters"),
    new WellKnownArtist("Talking Heads"),
    new WellKnownArtist("Cream"),
    new WellKnownArtist("Dire Straits"),
    new WellKnownArtist("Steve Miller Band"),
    new WellKnownArtist("blink-182"),
    new WellKnownArtist("Rage Against the Machine"),
    new WellKnownArtist("One Republic"),
    new WellKnownArtist("Miley Cyrus"),
    new WellKnownArtist("Ariana Grande"),
    new WellKnownArtist("Aretha Franklin"),
    new WellKnownArtist("Frank Sinatra"),
    new WellKnownArtist("Eminem"),
    new WellKnownArtist("Selena Gomez"),
    new WellKnownArtist("Dua Lipa"),
    new WellKnownArtist("Justin Bieber"),
    new WellKnownArtist("Billie Eilish"),
    new WellKnownArtist("David Bowie"),
    new WellKnownArtist("Whitney Houston"),
    new WellKnownArtist("Shawn Mendes"),
    new WellKnownArtist("Britney Spears"),
    new WellKnownArtist("Rihanna"),
    new WellKnownArtist("The Weekend"),
    new WellKnownArtist("P!nk", "Pink"),
    new WellKnownArtist("Demi Lovato"),
    new WellKnownArtist("Harry S tyles"),
    new WellKnownArtist("Jennifer Lopez"),
    new WellKnownArtist("Cher"),
    new WellKnownArtist("Doja Cat"),
    new WellKnownArtist("Drake"),
    new WellKnownArtist("Kelly Clarkson"),
    new WellKnownArtist("Sabrina Carpenter"),
    new WellKnownArtist("Shakira"),
    new WellKnownArtist("Justin Timberlake"),
    new WellKnownArtist("Celine Dion"),
    new WellKnownArtist("Lorde"),
    new WellKnownArtist("Olivia Rodrigo"),
    new WellKnownArtist("The Black Keys"),
    new WellKnownArtist("The White Stripes"),
    new WellKnownArtist("Jack White"),
    new WellKnownArtist("MGMT"),
    new WellKnownArtist("LCD Soundsystem"),
    new WellKnownArtist("Michael Jackson"),
    new WellKnownArtist("Wheezer"),
    new WellKnownArtist("Ellie Goulding"),
    new WellKnownArtist("Eric Clapton"),
    new WellKnownArtist("Chicago"),
    new WellKnownArtist("Backstreet Boys"),
    new WellKnownArtist("NSync"),
    new WellKnownArtist("Tupac"),
    new WellKnownArtist("Biggie"),
    new WellKnownArtist("Nickleback"),
    new WellKnownArtist("Santana"),
    new WellKnownArtist("Bob Seger"),
    new WellKnownArtist("Niki Minaj"),
    new WellKnownArtist("Sheryl Crow"),
    new WellKnownArtist("One Republic"),
    new WellKnownArtist("No Doubt"),
    new WellKnownArtist("Enrique Iglesias"),
    new WellKnownArtist("Boyz II Men"),
    new WellKnownArtist("3 Doors Down"),
    new WellKnownArtist("Bon Jovi"),
    new WellKnownArtist("Destiny's Child"),
    new WellKnownArtist("Calvin Harris"),
    new WellKnownArtist("Alanis Morissette"),
    new WellKnownArtist("Kesha"),
    new WellKnownArtist("Pitbull"),
    new WellKnownArtist("Avril Lavigne"),
    new WellKnownArtist("Matchbox Twenty"),
    new WellKnownArtist("Jason Derulo"),
    new WellKnownArtist("Third Eye Blind"),
    new WellKnownArtist("Yellowcard"),
    new WellKnownArtist("Foreigner"),
    new WellKnownArtist("Tim McGraw"),
    new WellKnownArtist("Shania Twain"),
    new WellKnownArtist("Lana Del Rey"),
    new WellKnownArtist("Glass Animals"),
    new WellKnownArtist("Mumford & Sons"),
    new WellKnownArtist("Alabama"),
    new WellKnownArtist("Cat Stevens"),
    new WellKnownArtist("Jimi Hendrix"),
    new WellKnownArtist("Garth Brooks"),
    new WellKnownArtist("Halsey"),
    new WellKnownArtist("Hootie & The Blowfish"),
    new WellKnownArtist("2Pac"),
    new WellKnownArtist("50 Cent"),
    new WellKnownArtist("Cheap Trick"),
    new WellKnownArtist("The Cranberries"),
    new WellKnownArtist("The Chemical Brothers"),
    new WellKnownArtist("The Smashing Pumpkins"),
    new WellKnownArtist("Snoop Dogg"),
    new WellKnownArtist("James Brown"),
    new WellKnownArtist("Miles Davis"),
    new WellKnownArtist("Ella Fitzgerald"),
    new WellKnownArtist("Steve Miller Band"),
    new WellKnownArtist("Sam Smith"),
    new WellKnownArtist("Rob Zombie"),
    new WellKnownArtist("R.E.M."),
    new WellKnownArtist("Poison"),
    new WellKnownArtist("Papa Roach"),
    new WellKnownArtist("The Cure"),
    new WellKnownArtist("Counting Crows"),
    new WellKnownArtist("The Clash"),
    new WellKnownArtist("Lynyrd Skynyrd"),
    new WellKnownArtist("Little Richard"),
    new WellKnownArtist("The Lumineers"),
    new WellKnownArtist("Tom Petty"),
    new WellKnownArtist("Toby Keith"),
    new WellKnownArtist("Queens of the Stone Age"),
    new WellKnownArtist("Lenny Kravitz"),
    new WellKnownArtist("Lil Wayne"),
    new WellKnownArtist("Louis Armstrong"),
    new WellKnownArtist("Prince"),
    new WellKnownArtist("B.B. King"),
    new WellKnownArtist("Alice Cooper"),
    new WellKnownArtist("Beck"),
    new WellKnownArtist("Billie Holiday"),
    new WellKnownArtist("Frank Zappa"),
    new WellKnownArtist("Method Man"),
    new WellKnownArtist("Jimmy Eat World"),
    new WellKnownArtist("Evanescence"),
    new WellKnownArtist("The Verve"),
    new WellKnownArtist("The Verve Pipe"),
    new WellKnownArtist("Owl City"),
    new WellKnownArtist("Meghan Trainor"),
    new WellKnownArtist("Paric! At the Disco"),
    new WellKnownArtist("Hamilton"),
    new WellKnownArtist("Franz Ferdinand"),
  };

  public static bool IsWellKnown(string name) {
    return List.Any(n => n.Name.ToLower().Equals(name.ToLower()));
  }

  public static string? Get(string name) {
    return List.FirstOrDefault(l => l.Matcher.IsMatch(name.ToLower()))?.Name;
  }

  public static async Task<Artist?> ChooseArtist(ITuneContext context, params string[] names) => names.Any() ? await GetArtist(context, names) : null;

  public static async Task<Artist> GetArtist(ITuneContext context, params string[] names) {
    if (!names.Any()) throw new InternalTuneException(context, "No artist names provided");
    List<string>? wellKnown = names.Select(Get).Where(a => a != null).Cast<string>().ToList();
    // context.Log.Information("[WK] {@names} => {@wk}", names, names.Select(Get));
    string? artistName = wellKnown.Any() ? wellKnown.First() : names.First();

    return await context.UpsertId<Artist>(artistName);
  }

  public static async Task<Collaborator> GetCollaborator(ITuneContext context, params string[] names) {
    if (!names.Any()) throw new InternalTuneException(context, "No artist names provided");
    List<string>? wellKnown = names.Select(Get).Where(a => a != null).Cast<string>().ToList();
    // context.Log.Information("[WK] {@names} => {@wk}", names, names.Select(Get));
    string? artistName = wellKnown.Any() ? wellKnown.First() : names.First();

    return await context.UpsertId<Collaborator>(artistName);
  }
}
