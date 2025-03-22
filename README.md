# Civitai Favorites Downloader

A C# command-line application that downloads all posts from your Civitai "Favorite Posts" collection, including all images.

## Features

- Authenticates with your Civitai API token
- Finds your "Favorite Posts" collection (or lets you choose any collection)
- Downloads all posts with pagination support
- For each post, downloads:
  - Post details as JSON
  - Post content as text
  - All images from the post

## Requirements

- .NET 6.0 or later
- A Civitai API token

## How to Get Your API Token

1. Log in to your Civitai account
2. Go to your account settings
3. Navigate to the API Keys section
4. Create a new API key with appropriate permissions (at minimum, read access to your collections)

## How to Use

1. Clone or download this repository
2. Build the application using .NET CLI or Visual Studio:
   ```
   dotnet build
   ```
3. Run the application:
   ```
   dotnet run
   ```
4. Follow the prompts:
   - Enter your API token
   - Specify an output directory (or leave blank to use the current directory)
   - The app will automatically find your "Favorite Posts" collection
   - If it can't find it, you'll be shown a list of your collections to choose from

## Output Structure

```
output_directory/
└── Favorite Posts/
    ├── [post_id]_[post_title]/
    │   ├── details.json
    │   ├── content.txt
    │   ├── image_0.jpg
    │   ├── image_1.jpg
    │   └── ...
    ├── [post_id]_[post_title]/
    │   └── ...
    └── ...
```

## How It Works

This application uses Civitai's tRPC API endpoints to:

1. Authenticate with your API token
2. Fetch your collections to find the "Favorite Posts" collection
3. Download posts from that collection with pagination
4. Save all post data and images locally

## Notes

- Images are saved with sequential numbers rather than their original filenames
- Post titles are sanitized to be valid directory names
- Console output shows progress as posts and images are downloaded
- The application requires internet access to connect to the Civitai API

## Troubleshooting

If you encounter issues:

1. Verify your API token is correct and has the necessary permissions
2. Check your internet connection
3. Ensure you haven't exceeded API rate limits
4. Look for error messages in the console output

## License

This project is licensed under the MIT License.