﻿using ChiitransLite.misc;
using ChiitransLite.settings;
using ChiitransLite.texthook;
using ChiitransLite.translation;
using ChiitransLite.translation.edict;
using ChiitransLite.translation.edict.parseresult;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ChiitransLite.forms {
    public partial class TranslationForm : Form {

        private HintForm hintForm;
        private BackgroundForm backgroundForm;
        private ParseResult lastParseResult;
        private ParseOptions lastParseOptions;
        private ParseResult lastSelectedParseResult;
        private int waitingForId = -1;
        private string lastSelection = null;
        private bool lastIsRealSelection = true;
        
        class InteropMethods {
            private TranslationForm form;

            public InteropMethods(TranslationForm form) {
                this.form = form;
            }

            public void dragForm() {
                form.sendSysCommand(Winapi.MOUSE_MOVE);
            }

            public void dblClickCaption() {
                if (form.WindowState == FormWindowState.Maximized) {
                    form.WindowState = FormWindowState.Normal;
                } else {
                    form.WindowState = FormWindowState.Maximized;
                }
            }

            public void resizeForm(int dx, int dy) {
                if (form.WindowState != FormWindowState.Normal) {
                    return;
                }
                uint msg = 0;
                switch (dx) {
                    case -1:
                        switch (dy) {
                            case -1:
                                msg = 0xF004;
                                break;
                            case 0:
                                msg = 0xF001;
                                break;
                            case 1:
                                msg = 0xF007;
                                break;
                        }
                        break;
                    case 0:
                        switch (dy) {
                            case -1:
                                msg = 0xF003;
                                break;
                            case 1:
                                msg = 0xF006;
                                break;
                        }
                        break;
                    case 1:
                        switch (dy) {
                            case -1:
                                msg = 0xF005;
                                break;
                            case 0:
                                msg = 0xF002;
                                break;
                            case 1:
                                msg = 0xF008;
                                break;
                        }
                        break;
                }
                if (msg != 0) {
                    form.sendSysCommand(msg);
                }
            }

            public void formMinimize() {
                form.WindowState = FormWindowState.Minimized;
            }

            public void formClose() {
                form.Close();
            }

            public void showHint(double parseId, double num, double x, double y, double h, double browserW, double browserH) {
                form.showHint((int)parseId, (int)num, x, y, h, browserW, browserH);
            }

            public void hideHint() {
                form.hideHint();
            }

            public bool onWheel(int units) {
                return form.hintForm.onWheel(units);
            }

            public void setTransparentMode(bool isEnabled) {
                form.setTransparentMode(isEnabled, false);
            }

            public void setTransparencyLevel(int level) {
                form.setTransparencyLevel(level);
            }

            public void setFontSize(int fontSize) {
                Settings.app.fontSize = fontSize;
            }

            public void showOptionsForm() {
                form.showOptionsForm();
            }

            public object getOptions() {
                return new {
                    transparentMode = Settings.app.transparentMode,
                    transparencyLevel = Settings.app.transparencyLevel,
                    fontSize = Settings.app.fontSize
                };
            }

            public bool toggleClipboardTranslation() {
                bool newValue = !Settings.app.clipboardTranslation;
                TranslationService.instance.setClipboardTranslation(newValue);
                return newValue;
            }

            public void showContextMenu(string selection, bool isRealSelection, int selectedParseResultId) {
                form.showContextMenu(selection, isRealSelection, selectedParseResultId);
            }
        }

        public TranslationForm() {
            InitializeComponent();
            hintForm = new HintForm();
            hintForm.setMainForm(this);
            backgroundForm = new BackgroundForm();
            backgroundForm.setMainForm(this);
            FormUtil.restoreLocation(this);
            webBrowser1.ObjectForScripting = new BrowserInterop(webBrowser1, new InteropMethods(this));
            webBrowser1.Url = Utils.getUriForBrowser("translation.html");
            TranslationService.instance.onAtlasDone += (id, text) => {
                webBrowser1.callScript("newTranslationResult", id, text);
            };
            TranslationService.instance.onEdictDone += (id, parse) => {
                lastParseResult = parse;
                if (id == waitingForId) {
                    waitingForId = -1;
                    return;
                }
                lastParseOptions = null;
                submitParseResult(parse);
            };
            TextOptionsForm.instance.VisibleChanged += (sender, e) => {
                if ((sender as Form).Visible) {
                    this.SuspendTopMostBegin();
                } else {
                    this.SuspendTopMostEnd();
                }
            };
        }

        private static TranslationForm _instance = null;
        
        public static TranslationForm instance {
            get {
                if (_instance == null) {
                    _instance = new TranslationForm();
                }
                return _instance;
            }
        }

        public void setCaption(string s) {
            Text = s;
        }

        internal void sendSysCommand(uint command) {
            Winapi.ReleaseCapture(webBrowser1.Handle);
            Winapi.DefWindowProc(this.Handle, Winapi.WM_SYSCOMMAND, (UIntPtr)command, IntPtr.Zero);
        }

        private void TranslationForm_FormClosing(object sender, FormClosingEventArgs e) {
            if (e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                this.Hide();
            }
        }

        public void showHint(int parseId, int num, double x, double y, double h, double browserW, double browserH) {
            WordParseResult part = TranslationService.instance.getParseResult(parseId, num);
            if (part != null) {
                Invoke(new Action(() => {
                    double qx = browserW == 0 ? 1 : webBrowser1.Width / browserW;
                    double qy = browserH == 0 ? 1 : webBrowser1.Height / browserH;
                    hintForm.display(part, webBrowser1.PointToScreen(new Point((int)Math.Round(x * qx), (int)Math.Round(y * qy))), (int)Math.Round(h * qy));
                }));
            }
        }

        public void hideHint() {
            Invoke(new Action(() => {
                hintForm.hideIfNotHovering();
            }));
        }

        private Keys lastKeyCode;
        private Keys lastModifiers;
        private long lastDate;

        private void webBrowser1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e) {
            long now = DateTime.Now.Ticks;
            if (e.KeyCode == lastKeyCode && e.Modifiers == lastModifiers && now - lastDate < 50 * TimeSpan.TicksPerMillisecond) {
                return;
            }
            lastKeyCode = e.KeyCode;
            lastModifiers = e.Modifiers;
            lastDate = now;
            if (e.Modifiers == Keys.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.Insert)) {
                updateLastSelection();
                copyToolStripMenuItem_Click(null, null);
            } else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.V ||
                  e.Modifiers == Keys.Shift && e.KeyCode == Keys.Insert) {
                pasteToolStripMenuItem_Click(null, null);
            /*} else if (e.Modifiers == Keys.None && e.KeyCode == Keys.F5) {
                webBrowser1.Refresh();*/
            } else if (e.Modifiers == Keys.None && e.KeyCode == Keys.Space) {
                updateLastSelection();
                parseSelectionToolStripMenuItem_Click(null, null);
            } else if (e.Modifiers == Keys.None && e.KeyCode == Keys.Enter) {
                updateLastSelection();
                translateSelectionToolStripMenuItem_Click(null, null);
            } else if (e.Modifiers == Keys.None && e.KeyCode == Keys.Insert) {
                updateLastSelection();
                addNewNameToolStripMenuItem_Click(null, null);
            } else if (e.Modifiers == Keys.None && e.KeyCode == Keys.T) {
                transparentModeToolStripMenuItem_Click(null, null);
            } else if (e.Modifiers == Keys.None && e.KeyCode == Keys.O) {
                optionsToolStripMenuItem_Click(null, null);
            }
        }

        private void TranslationForm_Move(object sender, EventArgs e) {
            FormUtil.saveLocation(this);
            moveBackgroundForm();
        }

        internal void moveBackgroundForm() {
            if (Settings.app.transparentMode) {
                backgroundForm.updatePos();
            }
        }

        private void TranslationForm_Resize(object sender, EventArgs e) {
            FormUtil.saveLocation(this);
            moveBackgroundForm();
        }

        internal void updateReading(string text, string reading) {
            webBrowser1.callScript("updateReading", text, reading);
        }

        private string getSelection() {
            return cleanTextSelection((string)webBrowser1.callScript("getTextSelection"));
        }

        private string cleanTextSelection(string sel) {
            if (string.IsNullOrEmpty(sel)) {
                return null;
            }
            string res = Regex.Replace(sel, @"\u200B.*?\u200B", "");
            if (res == "") {
                return sel.Replace("\u200B", "");
            } else {
                return res;
            }
        }
        
        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e) {
            bool hasText = Clipboard.ContainsText();
            pasteToolStripMenuItem.Enabled = hasText;
            bool hasSelection = !string.IsNullOrWhiteSpace(lastSelection);
            parseSelectionToolStripMenuItem.Enabled = hasSelection && lastIsRealSelection;
            addNewNameToolStripMenuItem.Enabled = hasSelection;
            translateSelectionToolStripMenuItem.Enabled = hasSelection;
            transparentModeToolStripMenuItem.Checked = Settings.app.transparentMode;
            if (hasSelection && !lastIsRealSelection) {
                banWordToolStripMenuItem.Enabled = true;
                banWordToolStripMenuItem.Text = "Mark as incorrect: " + lastSelection;
            } else {
                banWordToolStripMenuItem.Enabled = false;
                banWordToolStripMenuItem.Text = "Mark as incorrect";
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e) {
            string sel = lastIsRealSelection ? lastSelection : null;
            if (sel != null) {
                Clipboard.SetText(sel);
            } else if (lastParseResult != null) {
                Clipboard.SetText(lastParseResult.asText());
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) {
            if (Clipboard.ContainsText()) {
                string fromClipboard = Clipboard.GetText();
                TranslationService.instance.update(fromClipboard, false);
            }
        }

        private void parseSelectionToolStripMenuItem_Click(object sender, EventArgs e) {
            string sel = lastIsRealSelection ? lastSelection : null;
            if (!string.IsNullOrWhiteSpace(sel)) {
                ParseResult pr = lastSelectedParseResult;
                if (pr != null) {
                    Settings.app.removeBannedWord(sel);
                    if (lastParseOptions == null) {
                        lastParseOptions = new ParseOptions();
                    }
                    lastParseOptions.addUserWord(sel);
                    waitingForId = pr.id;
                    TranslationService.instance.startParse(pr.id, pr.asText(), false, lastParseOptions).ContinueWith((res) => {
                        ParseResult newRes = res.Result;
                        if (newRes != null && !newRes.getParts().Any((p) => p.asText() == sel)) {
                            webBrowser1.callScript("flash", "No match found");
                        } else {
                            submitParseResult(newRes);
                        }
                    });
                }
            }
        }

        private void submitParseResult(ParseResult parse) {
            webBrowser1.callScript("newParseResult", parse.id, ParseResult.serializeResult(parse));
        }

        private ParseResult getSelectedParseResult() {
            object idObj = webBrowser1.callScript("getSelectedEntryId");
            if (idObj == null) {
                return null;
            } else {
                int id;
                if (idObj is int) {
                    id = (int)idObj;
                } else {
                    try {
                        id = (int)((double)idObj);
                    } catch {
                        Utils.info(idObj.GetType().ToString());
                        id = 0;
                    }
                }
                if (id == 0) {
                    return lastParseResult;
                } else {
                    return TranslationService.instance.getParseResult(id);
                }
            }
        }

        private void addNewNameToolStripMenuItem_Click(object sender, EventArgs e) {
            string sel = lastSelection;
            if (!string.IsNullOrWhiteSpace(sel)) {
                ParseResult pr = lastSelectedParseResult;
                EdictMatch oldName = Edict.instance.findName(sel);
                string oldSense = "";
                string oldNameType = null;
                if (oldName != null) {
                    EdictEntry oldNameEntry = oldName.findAnyName();
                    if (oldNameEntry != null) {
                        if (oldNameEntry.sense.Count > 0 && oldNameEntry.sense[0].glossary.Count > 0) {
                            oldSense = oldNameEntry.sense[0].glossary[0];
                        }
                        oldNameType = oldNameEntry.nameType;
                    }
                }
                if (oldSense == "" && sel.All((c) => TextUtils.isKana(c))) {
                    if (Settings.app.okuriganaType == OkuriganaType.RUSSIAN) {
                        oldSense = TextUtils.kanaToCyrillic(sel);
                    } else {
                        oldSense = TextUtils.kanaToRomaji(sel);
                    }
                    oldSense = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(oldSense);
                }
                UserNameForm userNameForm = new UserNameForm();
                this.hideHint();
                this.SuspendTopMost(() => {
                    moveBackgroundForm();
                    if (userNameForm.Open(sel, oldSense, oldNameType)) {
                        string key = userNameForm.getKey();
                        string sense = userNameForm.getSense();
                        string nameType = userNameForm.getNameType();
                        if (sense != "") {
                            Settings.session.addUserName(key, sense, nameType);
                        } else {
                            Settings.session.removeUserName(key);
                        }
                        if (pr != null) {
                            TranslationService.instance.startParse(pr.id, pr.asText(), true, null);
                        }
                    }
                });
                moveBackgroundForm();
            }
        }

        internal void setTransparentMode(bool isEnabled, bool propagateToClient = true) {
            Settings.app.transparentMode = isEnabled;
            if (isEnabled) {
                this.TransparencyKey = Color.FromArgb(0, 0, 1);
                this.BackColor = Color.FromArgb(0, 0, 1);
                moveBackgroundForm();
                this.backgroundForm.Show();
            } else {
                this.TransparencyKey = Color.Empty;
                this.BackColor = SystemColors.Window;
                this.backgroundForm.Hide();
            }
            if (propagateToClient) {
                webBrowser1.callScript("setTransparentMode", isEnabled);
            }
        }

        internal void setTransparencyLevel(int level) {
            Settings.app.transparencyLevel = level;
            backgroundForm.Opacity = level * 0.01;
        }

        private void TranslationForm_Shown(object sender, EventArgs e) {
            TranslationService.instance.setClipboardTranslation(Settings.app.clipboardTranslation);
            applyCurrentSettings();
        }

        private void transparentModeToolStripMenuItem_Click(object sender, EventArgs e) {
            setTransparentMode(!Settings.app.transparentMode);
        }

        private void showOptionsForm() {
            TextOptionsForm.instance.updateAndShow();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e) {
            showOptionsForm();
        }

        private void TranslationForm_VisibleChanged(object sender, EventArgs e) {
            if (Visible) {
                setTransparentMode(Settings.app.transparentMode);
            } else {
                backgroundForm.Hide();
            }
        }

        internal void applyCurrentSettings() {
            webBrowser1.callScript("applyTheme", Settings.app.cssTheme);
            webBrowser1.callScript("setSeparateWords", Settings.app.separateWords);
            hintForm.applyTheme(Settings.app.cssTheme);
        }

        internal void showContextMenu(string selection, bool isRealSelection, int selectedParseResultId) {
            lastSelection = selection;
            lastIsRealSelection = isRealSelection;
            if (selectedParseResultId != 0) {
                lastSelectedParseResult = TranslationService.instance.getParseResult(selectedParseResultId);
            } else {
                lastSelectedParseResult = lastParseResult;
            }
            contextMenuStrip1.Show(Cursor.Position);
        }

        private void updateLastSelection() {
            lastSelection = getSelection();
            lastIsRealSelection = true;
            lastSelectedParseResult = getSelectedParseResult();
        }

        private void banWordToolStripMenuItem_Click(object sender, EventArgs e) {
            if (!lastIsRealSelection && !string.IsNullOrEmpty(lastSelection)) {
                ParseResult pr = lastSelectedParseResult;
                if (pr != null) {
                    EdictMatchType? matchType = lastSelectedParseResult.getMatchTypeOf(lastSelection);
                    if (matchType.HasValue) {
                        Settings.app.addBannedWord(lastSelection, matchType.Value);
                        TranslationService.instance.startParse(pr.id, pr.asText(), false, null);
                    }
                }
            }
        }

        private void translateSelectionToolStripMenuItem_Click(object sender, EventArgs e) {
            string sel = lastSelection;
            if (!string.IsNullOrWhiteSpace(sel)) {
                TranslationService.instance.update(sel, false);
            }
        }

        private void TranslationForm_Activated(object sender, EventArgs e) {
            moveBackgroundForm();
        }
    }
}
