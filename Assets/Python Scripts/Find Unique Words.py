import os
import re
import string
import unicodedata
import json
from collections import defaultdict
import nltk
from nltk.corpus import words as nltk_words

nltk.download('words', quiet=True)
ENGLISH_WORDS = set(word.lower() for word in nltk_words.words())

def expand_repetitions(text):
    expanded = []
    for line in text.splitlines():
        match = re.search(r'\(x(\d+)\)\s*$', line)
        if match:
            count = int(match.group(1))
            line = re.sub(r'\(x\d+\)\s*$', '', line).strip()
            expanded.extend([line] * count)
        else:
            expanded.append(line.strip())
    return '\n'.join(filter(None, expanded))


def clean_text(text):
    text = unicodedata.normalize('NFKD', text)
    text = text.encode('ASCII', 'ignore').decode('ASCII')
    text = text.lower()
    text = text.translate(str.maketrans('', '', string.punctuation))
    return set(
        word for word in re.findall(r'\b[a-zA-Z]{5,}\b', text)
        if word in ENGLISH_WORDS
    )


def sort_files_numerically(filenames):
    def extract_track_number(name):
        match = re.match(r'(\d+)', name)
        return int(match.group(1)) if match else float('inf')
    return sorted(filenames, key=lambda name: extract_track_number(name))


def process_album_directory(album_path, artist, album, json_dir):
    song_words = {}
    word_to_songs = defaultdict(set)

    print(f"\nProcessing album: {artist} - {album}")
    filenames = [
        f for f in os.listdir(album_path)
        if f.lower().endswith('.txt')
    ]
    sorted_files = sort_files_numerically(filenames)

    for filename in sorted_files:
        track_path = os.path.join(album_path, filename)
        with open(track_path, 'r', encoding='utf-8') as f:
            lyrics = expand_repetitions(f.read())

        title = os.path.splitext(filename)[0]
        title = re.sub(r'^\d+\s+', '', title).strip()
        song_words[title] = clean_text(lyrics)
        for word in song_words[title]:
            word_to_songs[word].add(title)

    unique_words = {}
    for title, words in song_words.items():
        unique = sorted([word for word in words if len(word_to_songs[word]) == 1])
        unique_words[title] = unique

    json_data = {
        "artist": artist,
        "album_title": album,
        "songs": [
            {
                "song_title": title,
                "unique_words": words
            }
            for title, words in unique_words.items()
        ]
    }

    output_file = os.path.join(json_dir, f"{artist} - {album}.json")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(json_data, f, indent=2)
    
    print(f"JSON results written to: {output_file}")


def main():
    cwd = os.getcwd()
    json_dir = os.path.join(cwd, 'JSON')
    os.makedirs(json_dir, exist_ok=True)

    for subdir in os.listdir(cwd):
        full_path = os.path.join(cwd, subdir)
        if os.path.isdir(full_path) and ' - ' in subdir:
            try:
                artist, album = map(str.strip, subdir.split(' - ', 1))
                process_album_directory(full_path, artist, album, json_dir)
            except Exception as e:
                print(f"Error processing '{subdir}': {e}")


if __name__ == '__main__':
    main()