const TargetHostname = "docs.s1servers.com";

export default {
    fetch(request: Request): Response {
        const targetUrl = new URL(request.url);
        targetUrl.protocol = "https:";
        targetUrl.hostname = TargetHostname;

        return new Response(null, {
            status: 301,
            headers: {
                "Cache-Control": "public, max-age=86400",
                Location: targetUrl.toString(),
            },
        });
    },
} satisfies ExportedHandler;
