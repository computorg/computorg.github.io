
# Organization website

This repository stores the source of Computorg.


## How to contribute to the website

- Install [quarto](https://quarto.org/docs/get-started/)

- Install DotNet SDK 8.0

    - Linux:
    ```bash
    sudo apt-get install dotnet-sdk-8.0
    ```
    - macOS:
    ```bash
    brew install dotnet
    ```

- Create an API key on github (personal access token)

    - Go to your settings
    - Then developer settings
    - Then create a github token

- Put the token in a file named `.env-secret` in the root of this repository

    ```bash
    GITHUB_TOKEN=your_github_token
    ```

- Now, you can compile the website with `quarto render .`
