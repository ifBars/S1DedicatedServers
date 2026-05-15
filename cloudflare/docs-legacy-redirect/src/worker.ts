const TargetHostname = "docs.s1servers.com";

export default {
    fetch(request: Request): Response {
        const targetUrl = new URL(request.url);
        targetUrl.protocol = "https:";
        targetUrl.hostname = TargetHostname;

        return Response.redirect(targetUrl.toString(), 301);
    },
} satisfies ExportedHandler;
