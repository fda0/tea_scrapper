using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;

return app_entry();

Image take_screenshot_as_image(WebDriver driver)
{
    var screenshot = driver.GetScreenshot();
    var mem_stream = new MemoryStream(screenshot.AsByteArray);
    var image = Image.FromStream(mem_stream);
    return image;
}

void save_screenshot(WebDriver driver, string path)
{
    var screenshot = driver.GetScreenshot();
    var mem_stream = new MemoryStream(screenshot.AsByteArray);
    using FileStream stream = new FileStream(path, FileMode.Create);
    mem_stream.WriteTo(stream);
}

void set_resolution(WebDriver driver, int browser_scaling, int width, int height)
{
    width *= browser_scaling;
    height *= browser_scaling;

    // set browser window dimensions
    driver.Manage().Window.Size = new Size(width, height);

    // client rect with website render is smaller than browser window
    // so resize it again to get desired client rect
    var image = take_screenshot_as_image(driver);
    int width_delta = width - image.Width;
    int height_delta = height - image.Height;
    driver.Manage().Window.Size = new Size(width + width_delta, height + height_delta);
    // @speed We could cache width_delta & height_delta if we cared about speed at all
    // Also there probably is a way to query client rect without taking screenshots 

    var image2 = take_screenshot_as_image(driver);
    if (image2.Width != width || image2.Height != height)
    {
        Console.WriteLine("Tried setting res: " + width + "x" + height + ", but got: " + image2.Width + "x" + image2.Height);
    }
}

Rectangle calc_crop_rect(Image image, double ratio_y, double ratio_height)
{
    if (ratio_height < 0)
    {
        ratio_height = 1 - ratio_y;
    }

    var result = new Rectangle(
        0, (int)Math.Round(ratio_y * image.Height),
        image.Width, (int)Math.Round(ratio_height * image.Height));
    return result;
}

Rectangle draw_fit_width(Graphics g, Bitmap bitmap, Image image, Rectangle image_crop, int offset_y)
{
    double ratio = (double)bitmap.Width / image_crop.Width;
    Rectangle image_target = new Rectangle(
        0, offset_y,
        (int)Math.Round(image_crop.Width * ratio), (int)Math.Round(image_crop.Height * ratio));

    g.DrawImage(image, image_target, image_crop, GraphicsUnit.Pixel);
    return image_target;
}

void remove_element(WebDriver driver, IWebElement element)
{
    driver.ExecuteScript("arguments[0].remove()", element);
}

void hide_element(WebDriver driver, IWebElement element)
{
    driver.ExecuteScript("arguments[0].setAttribute('style', 'visibility:hidden')", element);
}

void show_element(WebDriver driver, IWebElement element)
{
    driver.ExecuteScript("arguments[0].removeAttribute('style')", element);
}

double scan_non_white_row_from_top(Bitmap bitmap)
{
    int skip_scrollbar_width = 17;
    var white_color = Color.White.ToArgb();
    for (int y = 0; y < bitmap.Height; y++)
    {
        for (int x = 0; x < bitmap.Width - skip_scrollbar_width; x++)
        {
            var argb = bitmap.GetPixel(x, y).ToArgb();
            if (argb != white_color)
            {
                return (double)y / bitmap.Height;
            }
        }
    }
    return 0;
}
double scan_non_white_row_from_bottom(Bitmap bitmap)
{
    int skip_scrollbar_width = 17;
    var white_color = Color.White.ToArgb();
    for (int y = bitmap.Height-1; y >= 0; y--)
    {
        for (int x = 0; x < bitmap.Width - skip_scrollbar_width; x++)
        {
            var argb = bitmap.GetPixel(x, y).ToArgb();
            if (argb != white_color)
            {
                return (double)y / bitmap.Height;
            }
        }
    }
    return 0;
}

int app_entry()
{
    bool online_mode = true;
    bool cache_skip_if_dir_exist = true;
    bool headless = true;
    int browser_scaling = 2;

    ChromeDriver driver = null;
    if (online_mode)
    {
        var chromeOptions = new ChromeOptions();
        if (headless) chromeOptions.AddArgument("--headless");
        driver = new ChromeDriver(chromeOptions);
        set_resolution(driver, browser_scaling, 1100, 1100);
    }

    string shop_dir = "fiveoclock/";
    Directory.CreateDirectory(shop_dir);

    string logo_path = shop_dir + "five_logo.png";
    if (online_mode)
    {
        using (var client = new WebClient())
        {
            string logo_url = "https://fiveoclock.eu/wp-content/uploads/2021/06/png_logo_r_rgb_green.png";
            client.DownloadFile(logo_url, logo_path);
        }
    }
    Image logo_image = Image.FromFile(logo_path);

    string[] five_o_clock_teas = {
        "adasiowa-mietowka",
        "bancha-blue-sky",
        "fresh-w-saszetkach-do-dzbanka",
        "herbata-aromatyzowana-sencha-truskawki-z-imbirem",
        "japan-bancha-aiki",
        "kaledonia",
        "paris-paris",
        "rooibos-exotic",
        "rooibos-mniam-mniam",
        "tureckie-jablko",
        "vesper",
    };

    foreach (var tea in five_o_clock_teas)
    {
        string tea_dir = shop_dir + tea + "/";
        
        if (cache_skip_if_dir_exist)
        {
            if (Directory.Exists(tea_dir))
            {
                continue;
            }
        }

        Directory.CreateDirectory(tea_dir);
        string screen_path1 = tea_dir + tea + "_screen1.png";
        string screen_path2 = tea_dir + tea + "_screen2.png";

        if (online_mode)
        {
            string tea_address = "https://fiveoclock.eu/" + tea + "/";
            driver.Navigate().GoToUrl(tea_address);
            if (browser_scaling != 1)
            {
                driver.ExecuteScript("document.body.style.zoom = '" + browser_scaling * 100 + "%'");
            }

            // accept cookies and wait for cookie thing to disappear
            try
            {
                var accept_cookie_button = driver.FindElement(By.Id("cn-accept-cookie"));
                accept_cookie_button.Click();
                var wait = new WebDriverWait(driver, new TimeSpan(0, 0, 30));
                wait.Until(condition => !accept_cookie_button.Displayed);
            } catch { }

            // adjust the website
            var captcha = driver.FindElement(By.ClassName("grecaptcha-badge"));
            remove_element(driver, captcha);

            var header = driver.FindElement(By.ClassName("header-wrapper"));
            remove_element(driver, header);

            var page_title = driver.FindElement(By.ClassName("page-title"));
            hide_element(driver, page_title);

            var product_main = driver.FindElement(By.ClassName("product-main"));
            {
                var product_title = product_main.FindElement(By.ClassName("product-title"));
                driver.ExecuteScript("arguments[0].setAttribute('style', 'font-size: 6em; margin: 0.5em 0 0 0')", product_title);

                try
                {
                    var star_rating = product_main.FindElement(By.ClassName("woocommerce-product-rating"));
                    remove_element(driver, star_rating);
                } catch { }

                var price = product_main.FindElement(By.ClassName("price-wrapper"));
                remove_element(driver, price);

                var gallery = product_main.FindElement(By.ClassName("woocommerce-product-gallery__image"));
                {
                    try
                    {
                        var image_label_thing = gallery.FindElement(By.ClassName("yith-wcbm-badge-text"));
                        hide_element(driver, image_label_thing);
                    } catch { }
                }

                var product_desc = product_main.FindElement(By.ClassName("product-short-description"));
                {
                    driver.ExecuteScript("arguments[0].setAttribute('style', 'margin: 0px !important')", product_desc);

                    var upper_desc_paragraph = product_desc.FindElement(By.TagName("p"));
                    remove_element(driver, upper_desc_paragraph);
                }

                var buy_form = product_main.FindElement(By.TagName("form"));
                hide_element(driver, buy_form);
                try
                {
                    var tea_bag_sizes = product_main.FindElement(By.ClassName("variations"));
                    remove_element(driver, tea_bag_sizes);
                } catch { }

                var free_shipping_reminder = product_main.FindElement(By.ClassName("info_after_cart_wrapper"));
                remove_element(driver, free_shipping_reminder);

                var social_icons = product_main.FindElement(By.ClassName("social-icons"));
                remove_element(driver, social_icons);
            }

            var description_bottom = driver.FindElement(By.ClassName("woocommerce-left-content-description"));
            var desc_bottom_paragraphs = description_bottom.FindElements(By.TagName("p"));
            foreach (var p in desc_bottom_paragraphs)
                driver.ExecuteScript("arguments[0].setAttribute('style', 'font-size:1.35em')", p);

            var reviews = driver.FindElement(By.ClassName("woocommerce-left-content-reviews"));
            hide_element(driver, reviews);

            var bottom_content = driver.FindElement(By.ClassName("woocommerce-content"));
            driver.ExecuteScript("arguments[0].setAttribute('style', 'width:95%')", bottom_content);


            // take screenshots
            var product_footer = driver.FindElement(By.ClassName("product-footer"));
            hide_element(driver, product_footer); // temporarily bottom of the page
            save_screenshot(driver, screen_path1);

            // hide upper page + scroll page
            show_element(driver, product_footer); // re-show bottom of the page
            hide_element(driver, product_main);
            new Actions(driver).ScrollToElement(desc_bottom_paragraphs.Last()).Perform();
            save_screenshot(driver, screen_path2);
        }

        if (true) {
            var image1 = (Bitmap)Image.FromFile(screen_path1);
            double image1_y = scan_non_white_row_from_top(image1);
            double image1_height = scan_non_white_row_from_bottom(image1) - image1_y;
            var image1_crop = calc_crop_rect(image1, image1_y, image1_height);
            
            var image2 = (Bitmap)Image.FromFile(screen_path2);
            var image2_crop = calc_crop_rect(image2, scan_non_white_row_from_top(image2), -1);

            int dim_ratio = 10;
            int dim_x = 148 * dim_ratio;
            int dim_y = 210 * dim_ratio;
            Bitmap bitmap = new Bitmap(dim_x, dim_y);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // fill backgroud
                g.FillRectangle(new SolidBrush(Color.White), new Rectangle(0, 0, bitmap.Width, bitmap.Height));

                int pos_y = 0;
                pos_y += (int)(bitmap.Height * 0.01);
                pos_y += draw_fit_width(g, bitmap, image1, image1_crop, pos_y).Height;
                pos_y += (int)(bitmap.Height * 0.025);
                pos_y += draw_fit_width(g, bitmap, image2, image2_crop, pos_y).Height;

                // hide scrollbars
                int scrollbar_width = (int)(0.01 * bitmap.Width);
                g.FillRectangle(new SolidBrush(Color.White), new Rectangle(bitmap.Width - scrollbar_width, 0, scrollbar_width, bitmap.Height));

                // draw logo
                {
                    var image = logo_image;
                    var image_crop = calc_crop_rect(logo_image, 0, 1);

                    double ratio = (double)bitmap.Width*0.5 / image_crop.Width;
                    int target_width = (int)Math.Round(image_crop.Width * ratio);
                    int target_height = (int)Math.Round(image_crop.Height * ratio);
                    Rectangle image_target = new Rectangle(
                        bitmap.Width - target_width, bitmap.Height - target_height,
                        target_width, target_height);
                    
                    g.DrawImage(image, image_target, image_crop, GraphicsUnit.Pixel);
                }
            }

            bitmap.Save(tea_dir + tea + ".png", ImageFormat.Png);
        }
    }

    if (online_mode)
    {
        driver.Quit();
    }
    return 2;
}

