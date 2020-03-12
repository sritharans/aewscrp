//////////////////////////////////////////////////////////////////////////////////////////////////////////
// About: This is a web scraping tool to download an AliExpress item listing and
//        extract similar listings to the item from other AliExpress merchants.
//        The output contains the description, URL and pricing of other similar listings.
//        The data could then be imported to perform price comparisons and other analysis.
//
// Authors: Sritharan Sivaguru (WQD180086)
//          Mohamed Fathi Mohamed (WQD180081)
//
// Learn more about F# at http://fsharp.org

open System
open PuppeteerSharp
open FSharp.Data

// CSV Schema that will be used for output later
type ProductInfo = CsvProvider<Sample = "Description, URL, Price",
                               Schema = "string, string, string",
                               HasHeaders=true>

// Get the HTNL data from an AliExpress listing
let GetAEListing url = async {
    // Download the Puppeteer Chrome browser
    do! BrowserFetcher().DownloadAsync BrowserFetcher.DefaultRevision |> Async.AwaitTask |> Async.Ignore

    // Set the browser to be headless (invisible)
    let options = LaunchOptions ()
    options.Headless <- true
    
    // Simulate user browsing activity to load the similar items listing
    use! browser = Puppeteer.LaunchAsync options |> Async.AwaitTask
    use! page = browser.NewPageAsync () |> Async.AwaitTask
    do! page.GoToAsync (url, WaitUntilNavigation.Networkidle0) |> Async.AwaitTask |> Async.Ignore
    do! page.ClickAsync ("#root") |> Async.AwaitTask |> Async.Ignore
    do! page.EvaluateExpressionAsync "window.scrollBy(0, document.body.scrollHeight);" |> Async.AwaitTask |> Async.Ignore
    do! page.WaitForSelectorAsync ".other-store-more" |> Async.AwaitTask |> Async.Ignore
    do! page.HoverAsync ".other-store-more" |> Async.AwaitTask |> Async.Ignore

    // Return the HTML data captured by the browser
    return! page.GetContentAsync () |> Async.AwaitTask
}

[<EntryPoint>]
let main argv =
    match argv.Length with
    | 2 -> printfn "\n\tDownloading AliExpress listing data:"
           printfn "\t%s" argv.[0]
           let output = GetAEListing argv.[0] |> Async.RunSynchronously
           // Parse the HTML data and filter out unwanted tags
           printfn "\tParsing HTML for Item listings..."
           let doc = HtmlDocument.Parse(output)
           let imore = doc.CssSelect("div.other-store-more div.item-info a")
           let pmore = doc.CssSelect("div.other-store-more div.item-info div.item-price span")
           // Parse the filtered HTML tags and extract the related item data
           printfn "\tGenerating CSV data of item listing..."
           let pinfo = new ProductInfo((imore, pmore)
           ||> Seq.map2 (fun i p ->
                let ia = "https:" + i.AttributeValue("href")
                let pi = p.InnerText().Trim()
                (ProductInfo.Row(i.InnerText().Trim(), ia.Remove(ia.IndexOf('?')), pi.Substring(pi.IndexOf('$')+1)))))
           // Save the generated CSV output into a file
           printfn "\tSaving output into: %s" argv.[1]
           pinfo.Save(argv.[1])
    | _ -> printfn "\tURL and CSV file name required."
           printfn "\tExample: dotnet run https://www.aliexpress.com/item/xxx.html output.csv"
    0 // return an integer exit code
