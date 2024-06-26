﻿using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ImageMagick;
using SuchByte.MacroDeck.GUI.CustomControls;
using SuchByte.MacroDeck.GUI.Dialogs;
using SuchByte.MacroDeck.Icons;
using SuchByte.MacroDeck.Language;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Profiles;
using SuchByte.MacroDeck.Properties;
using SuchByte.MacroDeck.Startup;
using Icon = SuchByte.MacroDeck.Icons.Icon;
using MessageBox = SuchByte.MacroDeck.GUI.CustomControls.MessageBox;

namespace SuchByte.MacroDeck.GUI;

public partial class IconSelector : DialogForm
{
    public Icon SelectedIcon;
    public IconPack SelectedIconPack;

    private IconPack? _selectIconPack;

    public IconSelector(IconPack? iconPack = null)
    {
        _selectIconPack = iconPack;
        InitializeComponent();
        btnImportIconPack.Text = LanguageManager.Strings.ImportIconPack;
        btnExportIconPack.Text = LanguageManager.Strings.ExportIconPack;
        btnCreateIcon.Text = LanguageManager.Strings.CreateIcon;
        btnImport.Text = LanguageManager.Strings.ImportIcon;
        btnDeleteIcon.Text = LanguageManager.Strings.DeleteIcon;
        lblManaged.Text = LanguageManager.Strings.IconSelectorManagedInfo;
        lblSizeLabel.Text = LanguageManager.Strings.Size;
        lblTypeLabel.Text = LanguageManager.Strings.Type;
        btnPreview.Radius = ProfileManager.CurrentProfile?.ButtonRadius ?? 0;
        btnGenerateStatic.Text = LanguageManager.Strings.GenerateStatic;
    }

    private void LoadIcons(IconPack iconPack, bool scrollDown = false)
    {
        iconList.Controls.Clear();
        foreach (var icon in iconPack.Icons)
        {
            Task.Run(() =>
            {
                var button = new RoundedButton
                {
                    Width = 100,
                    Height = 100,
                    BackColor = Color.FromArgb(35,35,35),
                    Radius = ProfileManager.CurrentProfile?.ButtonRadius ?? 0,
                    BackgroundImageLayout = ImageLayout.Stretch,
                    BackgroundImage = icon.IconImage
                };
                if (button.BackgroundImage.RawFormat.ToString().ToLower() == "gif")
                {
                    button.ShowGIFIndicator = true;
                }
                button.Tag = icon;
                button.Click += (_, _) =>
                {
                    SelectIcon(icon);
                };
                button.Cursor = Cursors.Hand;
                Invoke(() => iconList.Controls.Add(button));
            });
        }
        
        if (scrollDown && iconList.Controls.Count > 1)
        {
            iconList.ScrollControlIntoView(iconList.Controls[^1]);
        }
    }

    private void SelectIcon(Icon icon)
    {
        var previewImage = icon.IconImage;

        btnGenerateStatic.Visible = icon.IconImage.RawFormat.ToString().ToLower() == "gif";

        btnPreview.BackgroundImage = previewImage;
        lblSize.Text = $@"{Encoding.Unicode.GetByteCount(icon.IconBase64) / 1000:n0} kByte";
        lblType.Text = previewImage.RawFormat.ToString().ToUpper();

        SelectedIconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
        SelectedIcon = icon;
    }

    private void BtnImport_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Import icon",
            CheckFileExists = true,
            CheckPathExists = true,
            DefaultExt = "png",
            Multiselect = true,
            Filter = "Image files (*.png;*.jpg;*.bmp;*.gif)|*.png;*.jpg;*.bmp;*.gif"
        };
        if (openFileDialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        
        var iconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
        
        var isGif = Path.GetExtension(openFileDialog.FileNames.FirstOrDefault())?.ToLower() == "gif";
        
        var iconImportQuality = new IconImportQuality(isGif);
        if (iconImportQuality.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        
        Task.Run(() => SpinnerDialog.SetVisisble(true, this));
        var icons = new List<Image>();

        Task.Run(() =>
        {
            MacroDeckLogger.Info(GetType(), $"Starting importing {openFileDialog.FileNames.Length} icon(s)");
            if (iconImportQuality.Pixels == -1)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    try
                    {
                        icons.Add(Image.FromFile(file));
                        MacroDeckLogger.Trace(GetType(), "Original image loaded");
                    }
                    catch (Exception ex)
                    {
                        MacroDeckLogger.Error(GetType(), "Error while loading original image: " + ex.Message + Environment.NewLine + ex.StackTrace);
                    }
                }
            }
            else
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    try
                    {
                        MacroDeckLogger.Trace(GetType(), "Using Magick to resize image");
                        using (var collection = new MagickImageCollection(new FileInfo(file)))
                        {
                            collection.Coalesce();
                            Parallel.ForEach(collection, image =>
                            {
                                image.Resize(iconImportQuality.Pixels, iconImportQuality.Pixels);
                                image.Crop(iconImportQuality.Pixels, iconImportQuality.Pixels);
                            });
                                                
                            using (var ms = new MemoryStream())
                            {
                                collection.Write(ms);
                                icons.Add(Image.FromStream(ms));
                            }
                        }
                        MacroDeckLogger.Trace(GetType(), "Image successfully resized");
                    }
                    catch (Exception ex) 
                    {
                        MacroDeckLogger.Error(GetType(), "Failed to resize image: " + ex.Message + Environment.NewLine + ex.StackTrace);
                    }
                }
            }
            
            openFileDialog.Dispose();
            iconImportQuality.Dispose();

            if (iconPack == null)
            {
                MacroDeckLogger.Error(GetType(), "Icon pack was null");
                SpinnerDialog.SetVisisble(false, this);
                return;
            }
            MacroDeckLogger.Info(GetType(), $"Adding {icons.Count} icons to {iconPack.Name}");
            var gifIcons = new List<Image>();
            gifIcons.AddRange(icons.FindAll(x => x.RawFormat.ToString().ToLower() == "gif").ToArray());
            var convertGifToStatic = false;
            if (gifIcons.Count > 0)
            {
                Invoke(() =>
                {
                    using var msgBox = new MessageBox();
                    convertGifToStatic = msgBox.ShowDialog(LanguageManager.Strings.AnimatedGifImported, LanguageManager.Strings.GenerateStaticIcon, MessageBoxButtons.YesNo) == DialogResult.Yes;
                });
                MacroDeckLogger.Info(GetType(), "Convert gif to static? " + convertGifToStatic);
            }

            foreach (var icon in icons)
            {
                IconManager.AddIconImage(iconPack, icon);
                if (convertGifToStatic)
                {
                    using var ms = new MemoryStream();
                    icon.Save(ms, ImageFormat.Png);
                    var iconStatic = Image.FromStream(ms);
                    IconManager.AddIconImage(iconPack, iconStatic);
                }
                icon.Dispose();
            }

            Invoke(() => LoadIcons(iconPack, true));
            MacroDeckLogger.Info(GetType(), "Icons successfully imported");
            SpinnerDialog.SetVisisble(false, this);
        });
    }

    private void BtnOk_Click(object sender, EventArgs e)
    {
        if (SelectedIcon != null)
        {
            DialogResult = DialogResult.OK;
        }
        Close();
    }

    private void IconSelector_Shown(object sender, EventArgs e)
    {
        Application.DoEvents();
        LoadIconPacks();
    }

    private void LoadIconPacks()
    {
        Invoke(() => iconPacksBox.Items.Clear());
        foreach (var iconPack in IconManager.IconPacks)
        {
            Invoke(new Action(() => iconPacksBox.Items.Add(iconPack.Name)));
        }
        if (Settings.Default.IconSelectorLastIconPack != null && IconManager.IconPacks.Count > 0)
        {
            Invoke(() =>
            {
                if (_selectIconPack != null)
                {
                    if (iconPacksBox.Items.Contains(_selectIconPack.Name))
                    {
                        iconPacksBox.SelectedIndex = iconPacksBox.FindStringExact(_selectIconPack.Name);
                        return;
                    }
                }

                iconPacksBox.SelectedIndex = iconPacksBox.Items.Contains(Settings.Default.IconSelectorLastIconPack)
                    ? iconPacksBox.FindStringExact(Settings.Default.IconSelectorLastIconPack)
                    : 0;
            });
        }
    }

    private void IconPacksBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        var iconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
        Settings.Default.IconSelectorLastIconPack = iconPack.Name;
        Settings.Default.Save();
        SelectedIconPack = iconPack;
        btnImport.Visible = !iconPack.ExtensionStoreManaged;
        btnCreateIcon.Visible = !iconPack.ExtensionStoreManaged;
        btnDeleteIcon.Visible = !iconPack.ExtensionStoreManaged;
        btnExportIconPack.Visible = !iconPack.ExtensionStoreManaged;
        lblManaged.Visible = iconPack.ExtensionStoreManaged;

        LoadIcons(iconPack);
    }

    private void BtnDeleteIconPack_Click(object sender, EventArgs e)
    {
        using var messageBox = new MessageBox();
        var iconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
        if (messageBox.ShowDialog(LanguageManager.Strings.AreYouSure, string.Format(LanguageManager.Strings.SelectedIconPackWillBeDeleted, iconPack.Name), MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            IconManager.DeleteIconPack(iconPack);
            btnPreview.BackgroundImage = null;
            SelectedIcon = null;
            lblSize.Text = "";
            lblType.Text = "";
            Task.Run(LoadIconPacks);
        }
    }

    private void BtnCreateIconPack_Click(object sender, EventArgs e)
    {
        using var createIconPackDialog = new CreateIconPack();
        if (createIconPackDialog.ShowDialog() == DialogResult.OK)
        {
                    
            Task.Run(LoadIconPacks);
            iconPacksBox.SelectedItem = createIconPackDialog.IconPackName;
        }
    }

    private void BtnImportIconPack_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Import icon pack",
            CheckFileExists = true,
            CheckPathExists = true,
            DefaultExt = ".macroDeckIconPack",
            Filter = "Macro Deck Icon Pack (*.macroDeckIconPack)|*.macroDeckIconPack|Zip Archive (*.zip)|*.zip",
        };
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            var importedIconPack = IconManager.InstallIconPackZip(openFileDialog.FileName);
            if (importedIconPack != null)
            {
                _selectIconPack = importedIconPack;
                LoadIconPacks();
            }
        }
    }

    private void BtnExportIconPack_Click(object sender, EventArgs e)
    {
        using var exportIconPackDialog = new ExportIconPackDialog(SelectedIconPack);
        if (exportIconPackDialog.ShowDialog() == DialogResult.OK)
        {
            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                IconManager.SaveIconPack(SelectedIconPack);
                IconManager.ExportIconPack(SelectedIconPack, folderBrowserDialog.SelectedPath);
                Cursor.Current = Cursors.Default;
                using var msgBox = new MessageBox();
                msgBox.ShowDialog(LanguageManager.Strings.ExportIconPack, string.Format(LanguageManager.Strings.XSuccessfullyExportedToX, SelectedIconPack.Name, folderBrowserDialog.SelectedPath), MessageBoxButtons.OK);
            }
        }
    }

    private void BtnDeleteIcon_Click(object sender, EventArgs e)
    {
        if (SelectedIcon == null || SelectedIconPack == null) return;
        using var messageBox = new MessageBox();
        if (messageBox.ShowDialog(LanguageManager.Strings.AreYouSure, string.Format(LanguageManager.Strings.SelectedIconWillBeDeleted, SelectedIconPack.Name), MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            IconManager.DeleteIcon(SelectedIconPack, SelectedIcon);
            btnPreview.BackgroundImage = null;
            btnPreview.Image = null;
            SelectedIcon = null;
            lblSize.Text = "";
            lblType.Text = "";
            LoadIcons(SelectedIconPack);
        }
    }

    private void BtnCreateIcon_Click(object sender, EventArgs e)
    {
        using var iconCreator = new IconCreator();
        if (iconCreator.ShowDialog() == DialogResult.OK)
        {
            using var iconImportQuality = new IconImportQuality();
            if (iconImportQuality.ShowDialog() == DialogResult.OK)
            {
                Image icon = null;
                if (iconImportQuality.Pixels == -1)
                {
                    icon = iconCreator.Image;
                }
                else
                {
                    iconCreator.Image.Save(Path.Combine(ApplicationPaths.TempDirectoryPath, "iconcreator"));

                    using var collection = new MagickImageCollection(new FileInfo(Path.GetFullPath(Path.Combine(ApplicationPaths.TempDirectoryPath, "iconcreator"))));
                    Cursor.Current = Cursors.WaitCursor;
                    collection.Coalesce();
                    foreach (var image in collection)
                    {
                        image.Resize(iconImportQuality.Pixels, iconImportQuality.Pixels);
                        image.Quality = 50;
                        image.Crop(iconImportQuality.Pixels, iconImportQuality.Pixels);
                    }
                    try
                    {
                        collection.Write(new FileInfo(Path.GetFullPath(Path.Combine(ApplicationPaths.TempDirectoryPath, "iconcreator.resized"))));
                        var imageBytes = File.ReadAllBytes(Path.Combine(ApplicationPaths.TempDirectoryPath, "iconcreator.resized"));
                        using var ms = new MemoryStream(imageBytes);
                        icon = Image.FromStream(ms);
                    } catch {}
                    Cursor.Current = Cursors.Default;
                }
                var iconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
                IconManager.AddIconImage(iconPack, icon);
                LoadIcons(iconPack, true);
            }
        }
    }

        

    private void btnDownloadIcon_Click(object sender, EventArgs e)
    {

    }

    private void BtnGenerateStatic_Click(object sender, EventArgs e)
    {
        var iconPack = IconManager.GetIconPackByName(iconPacksBox.Text);
        var icon = SelectedIcon.IconImage;
        var ms = new MemoryStream();
        icon.Save(ms, ImageFormat.Png);
        var bmpBytes = ms.GetBuffer();
        ms = new MemoryStream(bmpBytes);
        var iconStatic = Image.FromStream(ms);
        ms.Close();
        ms.Dispose();
        var addedIcon = IconManager.AddIconImage(iconPack, iconStatic);
        SelectIcon(addedIcon);
        LoadIcons(iconPack, true);

    }
}