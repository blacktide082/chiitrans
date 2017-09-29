using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChiitransLite.src.misc {
    class FuriganaUtils {

        private class WordPart {
            public bool isKana;
            public string text;

            public WordPart(bool isKana, string text) {
                this.isKana = isKana;
                this.text = text;
            }

            public static WordPart Kanji(string kanji) => new WordPart(false, kanji);

            public static WordPart Kana(string kanji) => new WordPart(true, kanji);

        }

        private class WordPartWithFurigana {
            public bool isKana;
            public string text;
            public string furigana;

            public WordPartWithFurigana(bool isKana, string text, string furigana) {
                this.isKana = isKana;
                this.text = text;
                this.furigana = furigana;
            }

            public static WordPartWithFurigana Kanji(string kanji, string furigana) => 
                new WordPartWithFurigana(false, kanji, furigana);

            public static WordPartWithFurigana Kana(string kana) =>
                new WordPartWithFurigana(true, kana, kana);

        }

        public static bool isKanji(char c) => Regex.IsMatch(c.ToString(), @"\p{IsCJKUnifiedIdeographs}");
        private static bool isKana(char c) => Regex.IsMatch(c.ToString(), @"\p{IsHiragana}|\p{IsKatakana}");


        private static List<WordPart> partition(string word) {
            List<WordPart> ret = new List<WordPart>();
            for(var i = 0; i < word.Length;) {
                StringBuilder builder = new StringBuilder();
                if(isKanji(word[i])) {
                    for(; i < word.Length && isKanji(word[i]); i++) {
                        builder.Append(word[i]);
                    }
                    ret.Add(WordPart.Kanji(builder.ToString()));
                } else if(isKana(word[i])) {
                    for (; i < word.Length && isKana(word[i]); i++) {
                        builder.Append(word[i]);
                    }
                    ret.Add(WordPart.Kana(builder.ToString()));
                } else {
                    //skip silently (may not be the best course of action
                    i++;
                }
            }
            return ret;
        }

        private static List<WordPartWithFurigana> groupWithFurigana(List<WordPart> word, string reading) {
            List<WordPartWithFurigana> ret = new List<WordPartWithFurigana>();
            while (word.Count > 0) {
                var first = word.First();
                if (first.isKana) {
                    if (reading.StartsWith(first.text)) {
                        word.RemoveAt(0);
                        reading = reading.Substring(first.text.Length);
                        ret.Add(WordPartWithFurigana.Kana(first.text));
                    } else return null;
                } else {
                    string prefix = reading[0].ToString();
                    reading = reading.Substring(1);
                    word.RemoveAt(0);
                    while(reading.Length > 0) {
                        var res = groupWithFurigana(new List<WordPart>(word.AsEnumerable()), reading);
                        if(res == null) {
                            prefix += reading[0];
                            reading = reading.Substring(1);
                        }
                        if(res != null || reading.Length == 0){ 
                            ret.Add(WordPartWithFurigana.Kanji(first.text, prefix));
                            if(res != null) ret.AddRange(res);
                            return ret;
                        }
                    }
                }
            }
            if (reading.Length == 0)
                return ret;
            else
                return null;
        }

        public static string generateFurigana(string word, string reading) {
            var strings = groupWithFurigana(partition(word), reading).
                Select(x => x.isKana ? x.text : $" {x.text}[{x.furigana}]");
            return string.Join("", strings).Trim();
        }

    }
}
