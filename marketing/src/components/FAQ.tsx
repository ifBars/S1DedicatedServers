import { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import { Plus } from "lucide-react";

const faqs = [
  {
    q: "What is S1DedicatedServers?",
    a: "An open-source mod that adds authoritative, headless dedicated server support to Schedule I — with admin tooling, remote console, and a public mod API.",
  },
  {
    q: "Is this official?",
    a: "No. This is a community-built project. It is not affiliated with, endorsed by, or officially connected to the developers or publishers of Schedule I.",
  },
  {
    q: "Do I need the base game?",
    a: "Yes. A legitimate copy of Schedule I is required. The mod extends multiplayer capabilities but does not include the game.",
  },
  {
    q: "Is it stable?",
    a: "Currently in beta. Core features — dedicated hosting, admin tools, save/load, mod API — are functional and actively used, with ongoing improvements.",
  },
  {
    q: "Can I build extensions?",
    a: "Yes. Both server-side and client-side mod APIs are available with lifecycle hooks, persistence integration, and custom messaging.",
  },
  {
    q: "Where can I get help?",
    a: "Check the documentation on GitHub, open an issue, or join community discussions linked from the repository.",
  },
];

const FAQ = () => {
  const [open, setOpen] = useState<number | null>(null);
  const { ref, visible } = useScrollReveal(0.1);

  return (
    <section id="faq" className="py-32 md:py-40">
      <div ref={ref} className="container px-4 md:px-8 max-w-3xl mx-auto">
        <motion.h2
          className="text-4xl md:text-5xl font-bold tracking-tight mb-16"
          initial={{ opacity: 0, y: 30 }}
          animate={visible ? { opacity: 1, y: 0 } : {}}
          transition={{ duration: 0.8, ease: [0.22, 1, 0.36, 1] }}
        >
          FAQ
        </motion.h2>

        <div className="space-y-0">
          {faqs.map(({ q, a }, i) => (
            <motion.div
              key={i}
              className="border-t border-border/30"
              initial={{ opacity: 0 }}
              animate={visible ? { opacity: 1 } : {}}
              transition={{ duration: 0.4, delay: 0.1 + i * 0.05 }}
            >
              <button
                onClick={() => setOpen(open === i ? null : i)}
                className="flex items-center justify-between w-full py-5 text-left group"
              >
                <span className="font-medium text-sm text-foreground group-hover:text-primary transition-colors duration-200 pr-4">
                  {q}
                </span>
                <Plus
                  size={16}
                  className={`text-muted-foreground shrink-0 transition-transform duration-300 ${
                    open === i ? "rotate-45" : ""
                  }`}
                />
              </button>
              <AnimatePresence>
                {open === i && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: "auto", opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
                    className="overflow-hidden"
                  >
                    <p className="text-sm text-muted-foreground leading-relaxed pb-5 max-w-lg">
                      {a}
                    </p>
                  </motion.div>
                )}
              </AnimatePresence>
            </motion.div>
          ))}
          <div className="border-t border-border/30" />
        </div>
      </div>
    </section>
  );
};

export default FAQ;
