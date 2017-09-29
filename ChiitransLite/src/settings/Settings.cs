﻿using ChiitransLite.misc;
using ChiitransLite.translation.edict;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace ChiitransLite.settings {
    class Settings {
        private static Settings _app = new Settings();

        public static Settings app {
            get {
                return _app;
            }
        }

        protected Settings() {
            initSelectedPages();
            initBannedWords();
            initSelectedReadings();
            initSelectedTranslators();
        }

        private static volatile SessionSettings cachedSessionSettings = null;
        public static SessionSettings session {
            get {
                if (cachedSessionSettings == null) {
                    cachedSessionSettings = SessionSettings.getDefault();
                }
                return cachedSessionSettings;
            }
        }

        public readonly string JMdictPath = Path.Combine(Utils.getRootPath(), "data/JMdict.xml");
        public readonly string JMnedictPath = Path.Combine(Utils.getRootPath(), "data/JMnedict.xml");
        public readonly string ConjugationsPath = Path.Combine(Utils.getRootPath(), "data/Conjugations.txt");
        public readonly string ReplacementScriptPath = Path.Combine(Utils.getRootPath(), "data/names.txt");
        public readonly string SaveWordPath = Path.Combine(Utils.getRootPath(), "words.txt");

        private ConcurrentDictionary<string, int> selectedPages;
        private bool selectedPagesDirty = false;
        private ConcurrentDictionary<string, bool> bannedWords;
        private ConcurrentDictionary<string, bool> bannedWordsKana;
        private bool isBannedWordsDirty = false;
        private ConcurrentDictionary<string, string> selectedReadings;
        private bool isSelectedReadingsDirty = false;

        private List<string> translators = new List<string> { };
        private List<string> selectedTranslators;

        public T getProperty<T>(string prop) {
            T res;
            getProperty(prop, out res);
            return res;
        }

        public bool getProperty<T>(string prop, out T value) {
            try {
                object res = Properties.Settings.Default[prop];
                value = (T)res;
                return true;
            } catch (NullReferenceException) {
                value = default(T);
                return false;
            } catch (InvalidCastException) {
                value = default(T);
                return false;
            } catch (SettingsPropertyNotFoundException) {
                value = default(T);
                return false;
            }
        }

        public void setProperty<T>(string prop, T value) {
            Properties.Settings.Default[prop] = value;
        }

        public void save() {
            lock (this) {
                if (selectedPages != null && selectedPagesDirty) {
                    Properties.Settings.Default.selectedPages = Utils.getJsonSerializer().Serialize(selectedPages);
                    selectedPagesDirty = false;
                }
                if (bannedWords != null && isBannedWordsDirty) {
                    Properties.Settings.Default.bannedWords = Utils.getJsonSerializer().Serialize(bannedWords.Keys);
                    Properties.Settings.Default.bannedWordsKana = Utils.getJsonSerializer().Serialize(bannedWordsKana.Keys);
                    isBannedWordsDirty = false;
                }
                if (selectedReadings != null && isSelectedReadingsDirty) {
                    Properties.Settings.Default.selectedReadings = Utils.getJsonSerializer().Serialize(selectedReadings);
                    isSelectedReadingsDirty = false;
                }
                Properties.Settings.Default.selectedTranslators = Utils.getJsonSerializer().Serialize(selectedTranslators);
                Properties.Settings.Default.Save();
                SessionSettings.saveAll();
            }
        }

        private void initSelectedPages() {
            string selectedPagesJson = Properties.Settings.Default.selectedPages;
            try {
                selectedPages = Utils.getJsonSerializer().Deserialize<ConcurrentDictionary<string, int>>(selectedPagesJson);
                if (selectedPages == null) selectedPages = new ConcurrentDictionary<string, int>();
            } catch {
                selectedPages = new ConcurrentDictionary<string, int>();
            }
        }


        private void initSelectedReadings() {
            string selectedReadingsJson = Properties.Settings.Default.selectedReadings;
            try {
                selectedReadings = Utils.getJsonSerializer().Deserialize<ConcurrentDictionary<string, string>>(selectedReadingsJson);
                if (selectedReadings == null) selectedReadings = new ConcurrentDictionary<string, string>();
            } catch {
                selectedReadings = new ConcurrentDictionary<string, string>();
            }
        }

        private void initBannedWords() {
            string bannedWordsJson = Properties.Settings.Default.bannedWords;
            List<string> words;
            try {
                words = Utils.getJsonSerializer().Deserialize<List<string>>(bannedWordsJson);
                if (words == null) words = new List<string>();
            } catch {
                words = new List<string>();
            }
            bannedWords = new ConcurrentDictionary<string,bool>(words.ToDictionary((w) => w, (w) => true));
            string bannedWordsKanaJson = Properties.Settings.Default.bannedWordsKana;
            try {
                words = Utils.getJsonSerializer().Deserialize<List<string>>(bannedWordsKanaJson);
                if (words == null) words = new List<string>();
            } catch {
                words = new List<string>();
            }
            bannedWordsKana = new ConcurrentDictionary<string, bool>(words.ToDictionary((w) => w, (w) => true));
        }

        private void initSelectedTranslators() {
            string selectedTranslatorsJson = Properties.Settings.Default.selectedTranslators;
            try {
                selectedTranslators = Utils.getJsonSerializer().Deserialize<List<string>>(selectedTranslatorsJson);
            } catch {
                selectedTranslators = null;
            }
        }

        internal int getSelectedPage(string stem) {
            return selectedPages.GetOrDefault(stem, -1);
        }

        internal void setSelectedPage(string stem, int page) {
            selectedPages[stem] = page;
            selectedPagesDirty = true;
        }

        internal string getSelectedReading(string word) {
            return selectedReadings.GetOrDefault(word);
        }

        internal void setSelectedReading(string word, string reading) {
            selectedReadings[word] = reading;
            isSelectedReadingsDirty = true;
        }

        internal static void setCurrentSession(string exeName) {
            cachedSessionSettings = SessionSettings.getByExeName(exeName);
        }

        internal static void setDefaultSession() {
            cachedSessionSettings = SessionSettings.getDefault();
        }

        public OkuriganaType okuriganaType {
            get {
                OkuriganaType res;
                if (Enum.TryParse(Properties.Settings.Default.okuriganaType, out res)) {
                    return res;
                } else {
                    return OkuriganaType.NORMAL;
                }
            }
            set {
                Properties.Settings.Default.okuriganaType = value.ToString();
            }
        }

        public NameDictLoading nameDict {
            get {
                NameDictLoading res;
                if (Enum.TryParse(Properties.Settings.Default.nameDict, out res)) {
                    return res;
                } else {
                    return NameDictLoading.NAMES;
                }
            }
            set {
                Properties.Settings.Default.nameDict = value.ToString();
            }
        }

        public TranslationDisplay translationDisplay {
            get {
                TranslationDisplay res;
                if (Enum.TryParse(Properties.Settings.Default.translationDisplay, out res)) {
                    return res;
                } else {
                    return TranslationDisplay.BOTH;
                }
            }
            set {
                Properties.Settings.Default.translationDisplay = value.ToString();
            }
        }

        public bool clipboardTranslation {
            get {
                return Properties.Settings.Default.clipboardTranslation;
            }
            set {
                Properties.Settings.Default.clipboardTranslation = value;
            }
        }

        public NonJapaneseLocaleWatDo nonJpLocale {
            get {
                NonJapaneseLocaleWatDo res;
                if (Enum.TryParse(Properties.Settings.Default.nonJpLocale, out res)) {
                    return res;
                } else {
                    return NonJapaneseLocaleWatDo.USE_LOCALE_EMULATOR;
                }
            }
            set {
                Properties.Settings.Default.nonJpLocale = value.ToString();
            }
        }

        public bool nonJpLocaleAsk {
            get {
                return Properties.Settings.Default.nonJpLocaleAsk;
            }
            set {
                Properties.Settings.Default.nonJpLocaleAsk = value;
            }
        }

        public string cssTheme {
            get {
                return Properties.Settings.Default.cssTheme;
            }
            set {
                Properties.Settings.Default.cssTheme = value;
            }
        }

        public bool separateWords {
            get {
                return Properties.Settings.Default.separateWords;
            }
            set {
                Properties.Settings.Default.separateWords = value;
            }
        }

        public bool separateSpeaker {
            get {
                return Properties.Settings.Default.separateSpeaker;
            }
            set {
                Properties.Settings.Default.separateSpeaker = value;
            }
        }

        public bool transparentMode {
            get {
                return Properties.Settings.Default.transparentMode;
            }
            set {
                Properties.Settings.Default.transparentMode = value;
            }
        }

        public double transparencyLevel {
            get {
                return Properties.Settings.Default.transparencyLevel;
            }
            set {
                Properties.Settings.Default.transparencyLevel = value;
            }
        }

        public double fontSize {
            get {
                return Properties.Settings.Default.fontSize;
            }
            set {
                Properties.Settings.Default.fontSize = value;
            }
        }

        internal void removeBannedWord(string word) {
            bool dontCare;
            isBannedWordsDirty = bannedWords.TryRemove(word, out dontCare) || isBannedWordsDirty;
            isBannedWordsDirty = bannedWordsKana.TryRemove(word, out dontCare) || isBannedWordsDirty;
        }

        internal void addBannedWord(string word, EdictMatchType matchType) {
            isBannedWordsDirty = (matchType == EdictMatchType.KANJI ? bannedWords : bannedWordsKana).TryAdd(word, true) || isBannedWordsDirty;
        }

        internal bool isWordBanned(string word, EdictMatchType matchType) {
            return (matchType == EdictMatchType.KANJI ? bannedWords : bannedWordsKana).ContainsKey(word);
        }

        internal void resetSelectedPages() {
            selectedPages.Clear();
            selectedPagesDirty = true;
        }

        internal void resetWordBans() {
            bannedWords.Clear();
            bannedWordsKana.Clear();
            isBannedWordsDirty = true;
        }

        internal void resetSelectedReadings() {
            selectedReadings.Clear();
            isSelectedReadingsDirty = true;
        }

        public bool atlasAsk {
            get {
                return Properties.Settings.Default.atlasAsk;
            }
            set {
                Properties.Settings.Default.atlasAsk = value;
            }
        }

        public bool ieUpgradeAsk {
            get {
                return Properties.Settings.Default.ieUpgradeAsk;
            }
            set {
                Properties.Settings.Default.ieUpgradeAsk = value;
            }
        }


        public bool stayOnTop {
            get {
                return Properties.Settings.Default.stayOnTop;
            }
            set {
                Properties.Settings.Default.stayOnTop = value;
            }
        }

        public bool clipboardJapanese {
            get {
                return Properties.Settings.Default.clipboardJapanese;
            }
            set {
                Properties.Settings.Default.clipboardJapanese = value;
            }
        }

        public string atlasEnv {
            get {
                string res = Properties.Settings.Default.atlasEnv;
                if (string.IsNullOrEmpty(res)) {
                    return "General";
                } else {
                    return res;
                }
            }
            set {
                Properties.Settings.Default.atlasEnv = value;
            }
        }

        public string ankiFieldNames {
            get {
                return Properties.Settings.Default.anki_field_names;
            }
            set {
                Properties.Settings.Default.anki_field_names = value;
            }
        }

        public string ankiSaveFormat {
            get {
                return Properties.Settings.Default.anki_save_format;
            }
            set {
                Properties.Settings.Default.anki_save_format = value;
            }
        }

        internal bool isShowTranslation() {
            return translationDisplay == TranslationDisplay.TRANSLATION || translationDisplay == TranslationDisplay.BOTH;
        }

        public void registerTranslators(List<string> translators) {
            this.translators = translators;
        }

        public List<string> getTranslators() {
            return translators;
        }

        public List<string> getSelectedTranslators(bool isAtlasPresent) {
            if (selectedTranslators == null) {
                if (isAtlasPresent) {
                    selectedTranslators = new List<string> { "ATLAS" };
                } else {
                    selectedTranslators = new List<string> { "Google" };
                }
            }
            return selectedTranslators.Intersect(translators).ToList();
        }

        public void setSelectedTranslators(List<string> selectedTranslators) {
            this.selectedTranslators = selectedTranslators;
        }
    }
}
