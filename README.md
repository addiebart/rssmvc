# rssmvc

A full-stack MVC RSS reader with support for user accounts, feeds with images, sorting, mobile displays, and more.

Written in C# using ASP.NET, Razor Pages, SQLite via EF Core, and TailwindCSS.

## Prerequisites

Install the following on the machine where you want to run the application:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (includes `npm`, required to rebuild the Tailwind CSS assets)

The application uses SQLite, so no separate database server is required. The database is created as `rssasp.db` in the project directory.

## Install

Clone the repository and open a terminal in the project directory:

```bash
git clone https://github.com/addiebart/rssmvc.git
cd rssmvc
```

Restore the .NET and frontend dependencies:

```bash
dotnet restore
npm ci
```

Install the Entity Framework Core command-line tool if it is not already installed:

```bash
dotnet tool install --global dotnet-ef
```

If the tool is already installed, update it instead:

```bash
dotnet tool update --global dotnet-ef
```

Apply the existing database migrations:

```bash
dotnet ef database update
```

Build the Tailwind CSS bundle:

```bash
npm run tailwind:build
```

## Run

Start the application with the HTTP launch profile:

```bash
dotnet run --launch-profile http
```

Open <http://localhost:5176> in a browser.


On the login page, choose **Register** to create the first local account. Feed subscriptions and articles are stored in the local SQLite database.

## Write-up
### Structure

Coming into this project, I knew I had a few options for my stack, even with the MVC framework requirement. While I considered using a full JavaScript stack, I ultimately decided to go with ASP.NET using its built in MVC template for two major reasons: 1) It is a very traditional implementation of the MVC pattern. Where often React apps are single page applications that are manipulated on the frontend in response to API calls made in JavaScript, I wanted to build an application where almost all of the elements are provided to the client upon loading the page. 2) I was already familiar with creating full-stack web applications using the .NET framework from my previous internship and as such, felt that familiarity would allow me to more effectively utilize AI tools and review their outputs.
I used SQLite and Entity Framework Core for database access for portability, speed, and integration with C# and I used Razor because it's native to .NET. 

The application follows the standard ASP.NET MVC pattern provided by the template. Program.cs configures dependency injection, authentication, and the EF Core database context. Controllers define endpoints, handling incoming requests. For example HomeController.cs loads the reader UI, AccountController.cs manages HTML and API endpoints for logins and registrations, and SubscriptionController.cs adds and removes feeds for the current user. Models represent database entities and view models, views render those models into HTML.

Database access is centralized in RssDbContext.cs. The "Migrations" directory contains the versioned EF Core schema changes used to create and update the SQLite database. Feed retrieval and article processing are kept in "Services". The background service automatically refreshes subscribed feeds every 15 minutes. Static frontend assets, including CSS, JavaScript, and image thumbnails are stored under "wwwroot".

### What I'd address with more time

#### Server Side Request Forgery

Currently, the application does not check the hostname provided by the user. While responses are checked for the correct Content-Type before being used, the request is still made regardless of the response, and a malicious actor could spoof the Content-Type header regardless. Imagine if a user requested to add a feed at http://[gatewayprivateip]/factoryreset. A thorough solution would need to check the actual data type and host at connection time after all DNS resolution and HTTP redirects, which I felt was outside of my scope for this project.

#### Pagination

If a user has many, many feeds, the resulting home page may be too much to load or display. This could be fixed by limiting the results on a single page to a certain number N and querying N results offset by N * (page number).


#### Last-Modified Header

Every feed refresh currently downloads and parses the full feed, even when nothing has changed since the last fetch. I would store the Last-Modified header from each feed's last successful response and send them back on the next request as If-Modified-Since. If the feed hasn't changed, the server would reply with 304 Not Modified and no body, and the app can skip parsing, thumbnail generation, and database writes for that feed, saving bandwidth on both ends. Notably, this would only work on GET requests for individual feeds.

### How I used AI

I used GitHub Copilot to write a majority of the code. I served more as a director of the agent and a reviewer of its work. Alongside independently deciding upon the stack and program structure, I would describe a specific function or UI component, review the agent's output, and iterate on its output as needed. I asked for a variety of artifacts from Copilot: UI elements, controller endpoint boilerplate, code to handle concurrency with semaphores, etc.

#### What I kept

- General UI design language
- Models
- Endpoint signatures
- Database definitions and migrations

#### Where I steered it

I didn’t rewrite many large blocks by hand. Most changes came from spotting something wrong in Copilot’s output and telling it what to do instead.

- Using a singleton semaphore to ensure that the database is only being written in one place at a time
- Storing settings for sorting, view mode using cookies rather than passing them as query parameters
- Only writing to the database once per feed refresh rather than twice, once for the text properties and again after the thumbnail was created.
- Creating web-optimized thumbnails in AVIF format to be displayed on the home page rather than raw images which can be tens of megabytes.

#### What I edited directly

- Smaller view edits (Headings, tailwind styles for small adjustments)
- Predicates for LINQ queries
- Timeout and interval constants
- Adding fallbacks for certain fields with the ?? operator
