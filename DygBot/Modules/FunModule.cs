using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DygBot.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DygBot.Modules
{
    [Summary("Commands made to be used by members")]
    public class FunModule : InteractiveBase<SocketCommandContext>
    {
        private readonly GitHubService _git;
        private readonly HttpClient _http;
        private readonly InteractiveService _interactive;
        private readonly LoggingService _logging;

        public FunModule(GitHubService git, HttpClient http, InteractiveService interactive, LoggingService logging)
        {
            _git = git;
            _http = http;
            _interactive = interactive;
            _logging = logging;
        }

        [Command("versus")]
        [Summary("Puts two images side to side to vote for one")]
        public async Task VersusUri([Summary("Address of first image")] Uri image1, [Summary("Address of second image")] Uri image2, [Summary("Opis do zdjęcia")][Remainder] string description)
        {
            using (Context.Channel.EnterTypingState())
            {
                try
                {
                    if (IsValidImage(image1) && IsValidImage(image2))
                    {
                        var img1 = await DownloadImage(image1);
                        var img2 = await DownloadImage(image2);

                        int smallerHeight = Math.Min(img1.Height, img2.Height);
                        img1.Mutate(x => x.Resize(smallerHeight / img1.Height * img1.Width, smallerHeight));
                        img2.Mutate(x => x.Resize(smallerHeight / img2.Height * img2.Width, smallerHeight));

                        using (var finalImg = new Image<Rgba32>(img1.Width + img2.Width + 10, smallerHeight))
                        {
                            for (int y = 0; y < img1.Height; y++)
                            {
                                for (int x = 0; x < img1.Width; x++)
                                {
                                    finalImg[x, y] = img1[x, y];
                                }
                            }

                            for (int y = 0; y < finalImg.Height; y++)
                            {
                                for (int x = 0; x < 10; x++)
                                {
                                    finalImg[x + img1.Width, y] = new Rgba32(255, 0, 0);
                                }
                            }

                            for (int y = 0; y < img2.Height; y++)
                            {
                                for (int x = 0; x < img2.Width; x++)
                                {
                                    finalImg[x + img1.Width + 10, y] = img2[x, y];
                                }
                            }

                            finalImg.Save("a_or_b.jpg", new JpegEncoder());
                        }

                        var message = await Context.Channel.SendFileAsync("a_or_b.jpg", description + $" (wysłane przez {Context.User})");
                        await message.AddReactionsAsync(new IEmote[] { new Emoji("🅰️"), new Emoji("🅱️") });

                        await Context.Message.DeleteAsync();

                    }
                    else
                    {
                        var reply = await ReplyAsync("Images are not valid");
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        await reply.DeleteAsync();
                        await Context.Message.DeleteAsync();
                    }
                }
                catch (Exception ex)
                {
                    await ReplyAndDeleteAsync("Miałem problem z tymi zdjęciami, spróbuj inne", timeout: TimeSpan.FromSeconds(3));
                    await _logging.OnLogAsync(new LogMessage(LogSeverity.Warning, "Discord", ex.Message, ex));
                    await Context.Message.DeleteAsync();
                }
            }
        }

        [Command("versus")]
        [Summary("Puts two images side to side to vote for one")]
        public async Task VersusImg([Summary("Opis do zdjęcia")][Remainder] string description)
        {
            using (Context.Channel.EnterTypingState())
            {
                Image<Rgba32> img1 = new Image<Rgba32>(1, 1);
                Image<Rgba32> img2 = new Image<Rgba32>(1, 1);

                var msg1 = Context.Message;

                try
                {
                    switch (msg1.Attachments.Count)
                    {
                        case 0:
                            await msg1.DeleteAsync();
                            await ReplyAndDeleteAsync("You didn't send any image", timeout: TimeSpan.FromSeconds(3));
                            break;
                        case 1:
                            {
                                img1 = await DownloadImage(new Uri(msg1.Attachments.First().Url));
                                await msg1.DeleteAsync();
                                var replyMsg1 = await ReplyAsync("Please send the second image");
                                var msg2 = await NextMessageAsync();

                                if (msg2 != null)
                                {
                                    if (msg2.Attachments.Count == 0)
                                    {
                                        await replyMsg1.DeleteAsync();
                                        await msg2.DeleteAsync();
                                        await ReplyAndDeleteAsync("Your message had no image", timeout: TimeSpan.FromSeconds(3));
                                        break;
                                    }
                                    else
                                    {
                                        img2 = await DownloadImage(new Uri(msg2.Attachments.First().Url));
                                        await msg2.DeleteAsync();
                                        await replyMsg1.DeleteAsync();
                                    }
                                }
                                else
                                {
                                    await replyMsg1.DeleteAsync();
                                    await ReplyAndDeleteAsync("You didn't send the image in time", timeout: TimeSpan.FromSeconds(3));
                                }
                                break;
                            }
                        case 2:
                            {
                                img1 = await DownloadImage(new Uri(msg1.Attachments.First().Url));
                                img2 = await DownloadImage(new Uri(msg1.Attachments.ElementAt(1).Url));
                                await msg1.DeleteAsync();
                                break;
                            }
                        default:
                            await msg1.DeleteAsync();
                            await ReplyAndDeleteAsync("Send maximum of 2 images", timeout: TimeSpan.FromSeconds(3));
                            return;
                    }

                    int smallerHeight = Math.Min(img1.Height, img2.Height);
                    img1.Mutate(x => x.Resize(smallerHeight / img1.Height * img1.Width, smallerHeight));
                    img2.Mutate(x => x.Resize(smallerHeight / img2.Height * img2.Width, smallerHeight));
                    int separatorWidth = (int)((img1.Width + img2.Width) * 0.01);
                    using (var finalImg = new Image<Rgba32>(img1.Width + img2.Width + separatorWidth, smallerHeight))
                    {
                        for (int y = 0; y < img1.Height; y++)
                        {
                            for (int x = 0; x < img1.Width; x++)
                            {
                                finalImg[x, y] = img1[x, y];
                            }
                        }

                        for (int y = 0; y < finalImg.Height; y++)
                        {
                            for (int x = 0; x < separatorWidth; x++)
                            {
                                finalImg[x + img1.Width, y] = new Rgba32(255, 0, 0);
                            }
                        }

                        for (int y = 0; y < img2.Height; y++)
                        {
                            for (int x = 0; x < img2.Width; x++)
                            {
                                finalImg[x + img1.Width + separatorWidth, y] = img2[x, y];
                            }
                        }

                        finalImg.Save("a_or_b.jpg", new JpegEncoder());
                    }

                    var message = await Context.Channel.SendFileAsync("a_or_b.jpg", description + $" (wysłane przez {Context.User})");
                    await message.AddReactionsAsync(new IEmote[] { new Emoji("🅰️"), new Emoji("🅱️") });
                }
                catch (Exception ex)
                {
                    await ReplyAndDeleteAsync("Miałem problem z tymi zdjęciami, spróbuj inne", timeout: TimeSpan.FromSeconds(3));
                    await _logging.OnLogAsync(new LogMessage(LogSeverity.Critical, "Discord", ex.Message, ex));
                    await msg1.DeleteAsync();
                }
            }
        }

        private bool IsValidImage(Uri imagePath)
        {
            var validExtensions = new string[] { "jpeg", "jpg", "png", "webp", "bmp", "tiff" };
            return validExtensions.Contains(imagePath.Segments.Last().Split('.').Last());
        }

        private async Task<Image<Rgba32>> DownloadImage(Uri imagePath)
        {
            var response = await _http.GetAsync(imagePath);
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(await response.Content.ReadAsStreamAsync());
            return image;
        }
    }
}
