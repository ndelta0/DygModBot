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
        public async Task VersusUri(Uri image1, Uri image2)
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

                    var message = await Context.Channel.SendFileAsync("a_or_b.png");
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
        public async Task VersusImg()
        {
            using (Context.Channel.EnterTypingState())
            {
                var msg1 = Context.Message;
                if (msg1.Attachments.Count == 0)
                {
                    await msg1.DeleteAsync();
                    await ReplyAndDeleteAsync("Your message had no image", timeout: TimeSpan.FromSeconds(3));
                }
                else
                {
                    var replyMsg1 = await ReplyAsync("Please send the second image");
                    var msg2 = await NextMessageAsync();
                    if (msg2 != null)
                    {
                        if (msg1.Attachments.Count == 0)
                        {
                            await msg1.DeleteAsync();
                            await replyMsg1.DeleteAsync();
                            await msg2.DeleteAsync();
                            await ReplyAndDeleteAsync("Your message had no image", timeout: TimeSpan.FromSeconds(3));
                        }
                        else
                        {
                            var img1 = await DownloadImage(new Uri(msg1.Attachments.First().Url));
                            var img2 = await DownloadImage(new Uri(msg2.Attachments.First().Url));

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

                            var message = await Context.Channel.SendFileAsync("a_or_b.png");
                            await message.AddReactionsAsync(new IEmote[] { new Emoji("🅰️"), new Emoji("🅱️") });

                            await msg1.DeleteAsync();
                            await replyMsg1.DeleteAsync();
                            await msg2.DeleteAsync();
                        }
                    }
                    else
                    {
                        await msg1.DeleteAsync();
                        await replyMsg1.DeleteAsync();
                        await ReplyAndDeleteAsync("You didn't send the second image in time", timeout: TimeSpan.FromSeconds(3));
                    }
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
            response.EnsureSuccessStatusCode();
            Image<Rgba32> image = (Image<Rgba32>)SixLabors.ImageSharp.Image.Load(await response.Content.ReadAsStreamAsync());
            return image;
        }
    }
}
