# FactBot

FactBot is a Discord bot that detects messages that contain misinformation, deletes them, and provides a detailed explanation. Misinformation is a serious problem on social media, especially in private communities such as small to medium-sized discord servers and private Facebook groups. The challenge that we plan to address is to extend fact checking to smaller and more private communities so that misinformation could have less of a chance to spread in “underground” areas.

Achieved 3rd place with a $500 award in the 2023 Canadian #AI Misinformation Hackathon.

Watch demo video [here](https://drive.google.com/file/d/1w38_pdo838rp1MnyrltMYieg5Jch34fg/view).

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Functionalities](#functionalities)
- [Effectiveness](#effectiveness)
- [License](#license)

## Installation

### Method 1
Invite the bot to your own server and try it out!

1. The invitation [link](https://discord.com/api/oauth2/authorize?client_id=1084285065493758123&permissions=8&scope=bot) of the bot is here. 
2. Please refer to usual guides on adding bots to Discord servers, such as this [article](https://www.selecthub.com/resources/how-to-add-bots-to-discord/).

### Method 2
Join our demo to see how the bot works before using it.

Link to demo [server](https://discord.gg/GnfWrMHy). 

## Usage

The bot could assist admins to fight misinformation within their Discord servers, as it is almost impossible to read all messages real-time in servers with a large active user base. 

## Functionalities

The bot uses a two-layer structure. 

### Layer 1 
Classifying messages in the server into four categories: `grammatical` , `ungrammatical` , `harmful`, and `unsure`. 

`grammatical`: Abbreviations, typos, and grammatical errors that does not impact comprehension are spared.  
`ungrammatical`: The message does not reasonably make sense, such as a series of completely random words.  
`harmful`: The message contains obviously harmful information, for example, "vaccines are harmful".  
`unsure`: The message cannot be confidently put into any of the above three categories.  

If a message is `harmful`, the bot would treat it as misinformation right away without going through the second layer.  
If a message is `ungrammatical`, it would not go through the second layer either, as that will only be a waste of resources.  
If a message is `grammatical` or `unsure`, it is passed to the second layer.

### Layer 2
Classifying messages in the server into four categories: `contains misinformation` , `does not contain misinformation` , `contains opinion`, and `unsure`.

`contains misinformation`: The message contains misinformation.  
`does not contain misinformation`: The message does not contain misinformation.  
`contains opinion`: The message is purely a matter of subjective opinion, and therefore cannot be classified as factually correct or wrong.  
`unsure`: The message cannot be confidently put into any of the above three categories.  

### Action
The bot deletes the messages that are classified as `harmful` or `contains misinformation`, and posts a response with explanation about why the message was deleted.

## Effectiveness

In our testings, the bot is effective in combating common forms of misinformation rampant on the internet, such as political slander, conspiracy theories, or misunderstanding of simple, popular scientific facts. 

However, it should be noted that the bot may not perform correctly when it comes to mathematical calculations, strictly logical deduction/induction, or scientific knowledge not usually present in popular media (a weakness of all GPT services since no actual "calculations" are performed). For example, it may respond incorrectly to "1 + 1 = 3". It also has very limited knowledge of events that happened after 2021. 

## License

This project uses the GNU GPLv3 [license](https://github.com/ai-misinformation-hackathon-2023/main/blob/main/LICENSE). 
