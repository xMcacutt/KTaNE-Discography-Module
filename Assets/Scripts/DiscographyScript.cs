using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Newtonsoft.Json;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[Serializable]
public class Song
{
    public string song_title;
    public List<string> unique_words;
}

[Serializable]
public class Album
{
    public string artist;
    public string album_title;
    public List<Song> songs;
}

public class DiscographyScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;
    public List<Album> albums;
    private static int _moduleIdCounter = 1;
    private int moduleId;
    public bool IsSolved;

    public Material surfaceMat;
    private Material instanceSurfaceMat;
    public Material contrastMat;
    private Material instanceContrastMat;
    public Material casingMat;
    private Material instanceCasingMat;
    public Texture2D[] artworks;
    public TextAsset[] jsonFiles;
    
    public Text text1;
    public Text text2;
    public List<string> words;
    private int selectedWordIndex = 0;

    public GameObject sticker;
    public GameObject[] stars;
    public Material starOnMat;
    public Material starOffMat;

    public KMSelectable leftButton;
    public KMSelectable rightButton;
    public KMSelectable submitButton;

    public MeshRenderer SurfaceRenderer;
    public MeshRenderer[] ContrastRenderers;
    public MeshRenderer[] CasingRenderers;

    private int skipCounter;
    private const int SkipThreshold = 3;
    private int rating;
    private bool isExplicit;
    private Vector2 initialTextPosition;
    private Coroutine scrollCoroutine;

    private Song startingSong;
    private Song targetSong;
    private string startingWord;
    private string targetWord;
    private Album album;

    public float resetLeftBound = -0.1f;
    public float resetRightBound = 0.1f;
    public float scrollStep = 0.015f;
    public float scrollInterval = 0.15f;

    void Awake()
    {
        InitializeModule();
        InitializeMaterials();
    }

    void Start()
    {
        Debug.Log("QWERQWERQWER " + jsonFiles.Length);
        albums = jsonFiles.Select(file => JsonConvert.DeserializeObject<Album>(file.text)).ToList();
        SetupButtonInteractions();
        GeneratePuzzle();
        scrollCoroutine = StartCoroutine(ScrollText());
    }

    // Used to test for errors
    // private int temp = 0;
    // void Update()
    // {
    //     if (albums == null)
    //         return;
    //     temp++;
    //     if (temp == 1)
    //     {
    //         GeneratePuzzle();
    //         temp = 0;
    //     }
    // }

    private void InitializeModule()
    {
        moduleId = _moduleIdCounter++;
    }

    private void InitializeMaterials()
    {
        instanceSurfaceMat = new Material(surfaceMat);
        instanceContrastMat = new Material(contrastMat);
        instanceCasingMat = new Material(casingMat);
        SurfaceRenderer.material = instanceSurfaceMat;
        foreach (var casingObject in CasingRenderers)
            casingObject.material = instanceCasingMat;
        foreach (var contrastObject in ContrastRenderers)
            contrastObject.material = instanceContrastMat;
    }

    private void SetupButtonInteractions()
    {
        bool didStrike;
        leftButton.OnInteract += () => HandleLeftButton(out didStrike);
        rightButton.OnInteract += () => HandleRightButton(out didStrike);
        submitButton.OnInteract += () => HandleSubmitButton(out didStrike);
    }

    private bool HandleLeftButton(out bool didStrike)
    {
        didStrike = false;
        if (IsSolved)
            return true;
        leftButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, leftButton.transform);
        skipCounter = 0;
        if (selectedWordIndex == 0) return false;
        selectedWordIndex--;
        UpdateTextDisplay();
        return false;
    }

    private bool HandleRightButton(out bool didStrike)
    {
        didStrike = false;
        if (IsSolved)
            return true;
        rightButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, rightButton.transform);
        if (selectedWordIndex == words.Count - 1)
        {
            skipCounter++;
            if (skipCounter < SkipThreshold) return false;
            if (rating == 0)
                GeneratePuzzle();
            else
            {
                Debug.LogFormat("[Discography #{0}] Strike! Tried to skip album with more than zero stars!", moduleId);
                Module.HandleStrike();
                didStrike = true;
            }
            return false;
        }
        selectedWordIndex++;
        UpdateTextDisplay();
        return false;
    }

    private bool HandleSubmitButton(out bool didStrike)
    {
        didStrike = false;
        submitButton.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, submitButton.transform);
        if (targetWord == words[selectedWordIndex])
        {
            text1.text = "";
            text2.text = "";
            IsSolved = true;
            Module.HandlePass();
            return true;
        }
        if (rating == 0)
            Debug.LogFormat("[Discography #{0}] Strike! Submitted word on 0 star album. Should have skipped.", moduleId);
        else
           Debug.LogFormat("[Discography #{0}] Strike! Submitted incorrect word: {1}", moduleId, words[selectedWordIndex]);
        Module.HandleStrike();
        didStrike = true;
        return false;
    }

    void UpdateTextDisplay()
    {
        StopCoroutine(scrollCoroutine);
        text1.text = text2.text = words[selectedWordIndex];
        float textWidth = text1.preferredWidth;
        RectTransform rect1 = text1.GetComponent<RectTransform>();
        RectTransform rect2 = text2.GetComponent<RectTransform>();
        rect1.anchoredPosition = new Vector2();
        rect2.anchoredPosition = initialTextPosition + new Vector2(textWidth * 2, 0f);
        scrollCoroutine = StartCoroutine(ScrollText());
    }

    private IEnumerator ScrollText()
    {
        RectTransform canvasRect = text1.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;
        RectTransform rect1 = text1.GetComponent<RectTransform>();
        RectTransform rect2 = text2.GetComponent<RectTransform>();
        Vector2 startPos = new Vector2(0, rect1.anchoredPosition.y);

        while (true)
        {
            text1.text = text2.text = words[selectedWordIndex];
            var textWidth = text1.preferredWidth;
            rect1.anchoredPosition = startPos;
            rect2.anchoredPosition = startPos + new Vector2(canvasWidth + textWidth / 2, 0f);
            
            yield return new WaitForSeconds(1f);

            while (true)
            {
                rect1.anchoredPosition -= new Vector2(scrollStep, 0f);
                rect2.anchoredPosition -= new Vector2(scrollStep, 0f);
                if (rect2.anchoredPosition.x <= 0f)
                {
                    rect1.anchoredPosition += new Vector2(canvasWidth + textWidth / 2, 0f);
                    rect2.anchoredPosition += new Vector2(canvasWidth + textWidth / 2, 0f);
                }
                yield return new WaitForSeconds(scrollInterval);
            }
        }
    }

    private void GeneratePuzzle()
    {
        Debug.LogFormat("[Discography #{0}] New generation...", moduleId);
        words = new List<string>();
        selectedWordIndex = 0;
        skipCounter = 0;
        SetupAlbumDisplay();
        SetupPuzzleLogic();
        if (rating == 0)
            Debug.LogFormat("[Discography #{0}] Album has 0 star rating. Skip.", moduleId);
        else 
            Debug.LogFormat("[Discography #{0}] Correct word to input: {1}", moduleId, targetWord);
    }

    private void SetupAlbumDisplay()
    {
        rating = Random.Range(0, 6);
        Debug.LogFormat("[Discography #{0}] Album Rating: {1}", moduleId, rating);
        for (var i = 0; i < 5; i++)
            stars[i].GetComponent<MeshRenderer>().material = i >= rating ? starOffMat : starOnMat;
        isExplicit = Random.Range(0, 2) == 0;
        Debug.LogFormat("[Discography #{0}] Is Explicit: {1}", moduleId, isExplicit);
        sticker.SetActive(isExplicit);
    }

    private void SetupPuzzleLogic()
    {
        var texture = artworks.PickRandom();
        var artistAlbum = ParseArtistAlbum(texture.name.Split('.')[0]);
        album = FindAlbum(artistAlbum[0], artistAlbum[1]);
        Debug.LogFormat("[Discography #{0}] Selected Album: {1} by {2}", moduleId, album.album_title, album.artist);
        var validSongs = album.songs.Where(s => s.unique_words.Any()).ToList();
        var offset = CalculateOffset();
        var orderedSongs = GetOrderedSongs(album.songs, rating);
        FindSongPair(validSongs, orderedSongs, offset);

        if (targetSong == null)
        {
            Debug.LogFormat("[Discography #{0}] No valid starting/target song pair found, regenerating...", moduleId);
            GeneratePuzzle();
            return;
        }

        PopulateWords(validSongs, orderedSongs);
        UpdateVisuals(texture, album, offset);
    }

    private int CalculateOffset()
    {
        var serial = Bomb.GetSerialNumber();
        var letter = serial.FirstOrDefault(char.IsLetter);
        var offset = char.ToUpper(letter) - 'A' + 1;
        Debug.LogFormat("[Discography #{0}] Serial # Offset: {1}->{2}", moduleId, letter, offset * (isExplicit ? -1 : 1));
        return isExplicit ? -offset : offset;
    }

    private void FindSongPair(List<Song> validSongs, List<Song> orderedSongs, int offset)
    {
        var shuffledSongs = validSongs.OrderBy(_ => Random.value).ToList();
        foreach (var potentialStartingSong in shuffledSongs)
        {
            var startIndices = orderedSongs
                .Select((song, idx) => new { Song = song, Index = idx })
                .Where(x => x.Song == potentialStartingSong)
                .Select(x => x.Index)
                .ToList();

            if (!startIndices.Any()) continue;

            foreach (var startIndex in startIndices)
            {
                var steps = Math.Abs(offset);
                var currentIndex = startIndex;
                var direction = offset >= 0 ? 1 : -1;
                var validSteps = 0;

                while (validSteps < steps && currentIndex >= 0 && currentIndex < orderedSongs.Count)
                {
                    currentIndex = (currentIndex + direction + orderedSongs.Count) % orderedSongs.Count;
                    if (orderedSongs[currentIndex] != potentialStartingSong)
                        validSteps++;
                }

                if (validSteps < steps) continue;

                var potentialTargetSong = orderedSongs[currentIndex];
                if (!potentialTargetSong.unique_words.Any()) continue;

                startingSong = potentialStartingSong;
                targetSong = potentialTargetSong;
            }
        }
    }

    private void PopulateWords(List<Song> validSongs, List<Song> orderedSongs)
    {
        if (rating != 0)
            Debug.LogFormat("[Discography #{0}] Song Word Mappings (In Order)", moduleId);
        var songWordMap = new Dictionary<Song, string>();
        foreach (var song in validSongs.Where(song => song.unique_words.Count > 0))
            songWordMap[song] = song.unique_words.PickRandom();

        startingWord = songWordMap[startingSong];
        targetWord = songWordMap[targetSong];

        if (rating != 0)
        {
            foreach (var song in orderedSongs)
            {
                string value;
                if (songWordMap.TryGetValue(song, out value))
                    Debug.LogFormat("[Discography #{0}] {1} -> {2} {3}", moduleId, song.song_title, value,
                        value == targetWord ? "[TARGET]" : value == startingWord ? "[START]" : "");
                else
                    Debug.LogFormat("[Discography #{0}] {1} -> [No unique words]", moduleId, song.song_title);
            }
        }

        var otherWords = validSongs
            .Where(s => s != startingSong && s != targetSong && songWordMap.ContainsKey(s))
            .Select(s => songWordMap[s])
            .ToList();
        otherWords.Add(targetWord);
        otherWords = otherWords.OrderBy(_ => Random.value).ToList();

        words.Clear();
        words.Add(startingWord);
        words.AddRange(otherWords);

        text1.text = words[selectedWordIndex];
        text2.text = words[selectedWordIndex];
    }

    private void UpdateVisuals(Texture2D texture, Album album, int offset)
    {
        instanceSurfaceMat.mainTexture = texture;
        instanceCasingMat.color = CalculateAverageColor(texture);
        instanceContrastMat.color = CalculateOppositeColor(instanceCasingMat.color);
    }

    private List<Song> GetOrderedSongs(List<Song> songs, int rating)
    {
        var result = new List<Song>();
        switch (rating)
        {
            case 0:
                return songs;
            case 1:
                result.AddRange(songs.Where((_, i) => i % 2 == 0));
                result.AddRange(songs.Where((_, i) => i % 2 != 0));
                break;
            case 2:
                for (int i = 0; i < (songs.Count + 1) / 2; i++)
                {
                    result.Add(songs[i]);
                    if (i != songs.Count - 1 - i)
                        result.Add(songs[songs.Count - 1 - i]);
                }
                break;
            case 3:
                result.AddRange(songs.AsEnumerable().Reverse());
                break;
            case 4:
                result.AddRange(songs);
                break;
            case 5:
                foreach (var song in songs)
                {
                    result.Add(song);
                    result.Add(song);
                }
                break;
        }

        if (result.Count <= 0) return result;
        var repeated = new List<Song>();
        for (int i = 0; i < Math.Max(1, (int)Math.Ceiling(10.0 / result.Count)); i++)
            repeated.AddRange(result);
        return repeated;
    }

    private Color CalculateAverageColor(Texture2D tex)
    {
        var pixels = tex.GetPixels();
        float r = 0, g = 0, b = 0, a = 0;
        foreach (var c in pixels)
        {
            r += c.r;
            g += c.g;
            b += c.b;
            a += c.a;
        }
        var total = pixels.Length;
        return new Color(r / total, g / total, b / total, a / total);
    }

    private Color CalculateOppositeColor(Color col)
    {
        return new Color(1 - col.r, 1 - col.g, 1 - col.b, col.a);
    }

    private string[] ParseArtistAlbum(string input)
    {
        var split = input.Split('-');
        return new string[] { split[0].Trim(), split[1].Trim() };
    }

    private Album FindAlbum(string artist, string albumTitle)
    {
        var result = albums.Find(a =>
            string.Equals(a.artist, artist, StringComparison.CurrentCultureIgnoreCase) &&
            string.Equals(a.album_title, albumTitle, StringComparison.CurrentCultureIgnoreCase));
        if (result == null)
            Debug.LogWarning("Album not found! " + artist + " - " + albumTitle);
        return result;
    }
    
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} input <string> [string of l, r, s], !{0} i <string>, !{0} in <string>, EXAMPLE: !1 input rrrs (right right right submit)";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var match = Regex.Match(command, @"^\s*(?:i|in|input)\s+([lrs]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
            yield break;
        yield return null;
        var input = match.Groups[1].Value;
        yield return StartCoroutine(PlayInputSequenceQueue(input));
        yield return null;
    }
	
    IEnumerator PlayInputSequenceQueue(string input)
    {
        foreach (var symbol in input)
        {
            bool didStrike;
            switch (symbol)
            {
                case 'l':
                    HandleLeftButton(out didStrike);
                    if (didStrike)
                        yield break;
                    break;
                case 'r':
                    HandleRightButton(out didStrike);
                    if (didStrike)
                        yield break;
                    break;
                case 's':
                    HandleSubmitButton(out didStrike);
                    if (didStrike)
                        yield break;
                    break;
            }
		
            yield return new WaitForSeconds(0.5f);
        }
    }
}