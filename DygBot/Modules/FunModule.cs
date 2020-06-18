using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DygBot.Services;
using SixLabors.ImageSharp;
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

        public FunModule(GitHubService git, HttpClient http, InteractiveService interactive)
        {
            _git = git;
            _http = http;
            _interactive = interactive;
        }

        [Command("versus")]
        [Summary("Puts two images side to side to vote for one")]
        public async Task VersusUri([Summary("Opis do zdjęcia")]string description, [Summary("Address of first image")]Uri image1, [Summary("Address of second image")] Uri image2)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (IsValidImage(image1) && IsValidImage(image2))
                {
                    var img1 = await DownloadImage(image1);
                    var img2 = await DownloadImage(image2);

                    int smallerHeight = Math.Min(img1.Height, img2.Height);
                    img1.Mutate(x => x.Resize(smallerHeight / img1.Height * img1.Width, smallerHeight));
                    img2.Mutate(x => x.Resize(smallerHeight / img2.Height * img2.Width, smallerHeight));

                    using var finalImg = new Image<Rgba32>(img1.Width + img2.Width + 10, smallerHeight);

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

                    finalImg.Save("a_or_b.png", new PngEncoder());

                    var message = await Context.Channel.SendFileAsync("a_or_b.png", description);
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
        }

        [Command("versus")]
        [Summary("Puts two images side to side to vote for one")]
        public async Task VersusImg([Summary("Opis do zdjęcia")] string description)
        {
            using (Context.Channel.EnterTypingState())
            {
                Image<Rgba32> img1 = new Image<Rgba32>(1, 1);
                Image<Rgba32> img2 = new Image<Rgba32>(1, 1);

                var msg1 = Context.Message;

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

                using var finalImg = new Image<Rgba32>(img1.Width + img2.Width + 10, smallerHeight);

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

                finalImg.Save("a_or_b.png", new PngEncoder());

                var message = await Context.Channel.SendFileAsync("a_or_b.png", description);
                await message.AddReactionsAsync(new IEmote[] { new Emoji("🅰️"), new Emoji("🅱️") });
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
            response.EnsureSuccessStatusCode();
            Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(await response.Content.ReadAsStreamAsync());
            return image;
        }
    }
}
