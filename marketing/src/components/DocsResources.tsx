import { motion } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import { ArrowUpRight } from "lucide-react";

const resources = [
  { title: "Installation Guide", href: "https://github.com/ifBars/S1DedicatedServers#installation", desc: "Step-by-step setup" },
  { title: "Configuration", href: "https://github.com/ifBars/S1DedicatedServers#configuration", desc: "Server settings & options" },
  { title: "Commands & Permissions", href: "https://github.com/ifBars/S1DedicatedServers#commands", desc: "Admin command reference" },
  { title: "Web Panel", href: "https://github.com/ifBars/S1DedicatedServers#web-panel", desc: "Localhost panel docs" },
  { title: "Authentication", href: "https://github.com/ifBars/S1DedicatedServers#authentication", desc: "Auth backend config" },
  { title: "Mod API Overview", href: "https://github.com/ifBars/S1DedicatedServers#api", desc: "Extension development" },
  { title: "Troubleshooting", href: "https://github.com/ifBars/S1DedicatedServers#troubleshooting", desc: "Common issues & fixes" },
  { title: "GitHub Repository", href: "https://github.com/ifBars/S1DedicatedServers", desc: "Source, issues, releases" },
];

const DocsResources = () => {
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="docs" className="py-32 md:py-40">
      <div ref={ref} className="container px-4 md:px-8 max-w-4xl mx-auto">
        <motion.div
          className="mb-16"
          initial={{ opacity: 0, y: 30 }}
          animate={visible ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
        >
          <h2 className="text-4xl md:text-5xl font-bold tracking-tight mb-4">
            Documentation
          </h2>
          <p className="text-muted-foreground text-lg">
            Everything you need to install, configure, and extend.
          </p>
        </motion.div>

        <div className="space-y-0">
          {resources.map(({ title, href, desc }, i) => (
            <motion.a
              key={title}
              href={href}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center justify-between py-4 border-t border-border/30 group transition-colors duration-200 hover:bg-secondary/10 -mx-4 px-4 rounded-sm"
              initial={{ opacity: 0, y: 10 }}
              animate={visible ? { opacity: 1, y: 0 } : {}}
              transition={{
                duration: 0.4,
                delay: 0.1 + i * 0.05,
                ease: [0.22, 1, 0.36, 1],
              }}
            >
              <div className="flex items-baseline gap-4">
                <span className="text-sm font-medium text-foreground group-hover:text-primary transition-colors duration-200">
                  {title}
                </span>
                <span className="text-xs text-muted-foreground hidden sm:inline">
                  {desc}
                </span>
              </div>
              <ArrowUpRight
                size={16}
                className="text-muted-foreground/40 group-hover:text-primary group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-all duration-200 shrink-0"
              />
            </motion.a>
          ))}
          <div className="border-t border-border/30" />
        </div>
      </div>
    </section>
  );
};

export default DocsResources;
