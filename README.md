
# Organization website

This repository stores the source of Computorg.


## How to contribute to the website:

- install quarto
- install dotnet-sdk-8.0

    ```bash
    sudo apt-get install dotnet-sdk-8.0
    ```

- create an API key on github

    - go to your settings
    - Then developer settings
    - Then create a github token

- Put the token in .env-secret in the root of this repository

    ```bash
    GITHUB_TOKEN=your_github_token
    ```

- Now, you can compile the website with 

    quarto-render
