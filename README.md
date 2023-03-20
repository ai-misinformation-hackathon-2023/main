# FactBot

FactBot is a discord bot that detects messages that contain misinformation and delete them. 

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Functionalities](#functionalities)
- [Effectiveness](#effectiveness)
- [License](#license)

## Installation

Method 1: Invite it to your own server

1. The invitation link  
2. Please follow usual guides on adding bots to discord servers, such as this [article](https://www.selecthub.com/resources/how-to-add-bots-to-discord/).

Method 2: If you would like to see how the bot works before using it

Join our demo [server](https://discord.gg/GnfWrMHy). 

## Usage

The bot could assist admins to fight misinformation within their discord servers, as it is almost impossible to read all messages real-time in servers with a large active user base. 

## Functionalities

The bot uses a two-layer structure. 

The first layer classifies messages in the server into four categories, `grammatical` , `ungrammatical` , `harmful`, and `unsure`. 

`grammatical`: abbreviations, typos, and grammatical errors that does not impact comprehension are spared.  
`ungrammatical`: the message does not reasonably make sense, such a series of completely random words.  
`harmful`: the message contains obviously harmful information, for example, "vaccines are harmful".  
`unsure`: the message cannot be confidently put into any of the above three categories.  

The second layer classifies messages in the server into four categories, `contains misinformation` , `does not contain misinformation` , `contains opinion`, and `unsure`.

`contains misinformation`: the message contains misinformation.  
`does not contain misinformation`: the message does not contain misinformation.  
`contains opinion`: the message is purely a matter of subjective opinion, and therefore it cannot be classified as factually correct or wrong.  
`unsure`: the message cannot be confidently put into any of the above three categories.  

If a message is `harmful`, the bot would treat it as misinformation right away without going through the second layer.  
If a message is `ungrammatical`, it would not go through the second layer either, as that will only be a waste of resources.  
If a message is `grammatical` or `unsure`, it is passed to the second layer.

Then, the bot deletes the messages that are classified as `harmful` or `contains misinformation`. 

## Effectiveness

In our testing, the bot is effective in combating common forms of misinformation rampant on the internect, such as political slander and conspiracy theories. 

However, it should be noted that the bot may not perform correctly when it comes to mathematical calculations, strictly logical deduction/induction, or scientific knowledge not usually present in popular media (a weakness of all GPT services). For example, it may respond incorrectly to "1 + 1 = 3". 

## License

This project uses the GNU GPLv3 [license](https://github.com/ai-misinformation-hackathon-2023/main/blob/main/LICENSE). 
