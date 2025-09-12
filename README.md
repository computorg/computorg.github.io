
# Organization website

This repository stores the source of Computorg.

## How to contribute to the website

### Quarto

Our website has been built with [Quarto](https://quarto.org), an open-source scientific and technical publishing system. The first thing you need to compile the website is therefore to install Quarto, which can be done by downloading the corresponing installer here: <https://quarto.org/docs/get-started/>.

## Positron

If you are using the new [Positron IDE](https://positron.posit.co), quarto is already bundled with it. You can simply type `which quarto` within the built-in terminal in Positron and add the returned path to your `PATH`.

### Microsoft DotNet SDK

You need to install Microsoft DotNet SDK which is now v9.0. Installers can be found here: <https://dotnet.microsoft.com/en-us/download>. Otherwise, you can install it on Unix systems via:

- Linux:
```bash
sudo apt-get install dotnet-sdk-9.0
```
- macOS:
```bash
brew install dotnet
```

### GitHub Personal Access Token

You need to connect to your GitHub account. 

- Go under *Settings*, scroll down until you see the *Developer settings* tab on the left sidebar and click on it. Follow the procedure to create a **classic** personal access token (PAT). Give it a name you will remember and remember to copy the PAT before closing the page.
- Put the token in a file named `.env-secret` in the root of this repository

```bash
GITHUB_TOKEN=your_github_token
```

Now, you can compile the website with `quarto render .`.
