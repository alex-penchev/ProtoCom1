# ProtoCom1

  :heavy_exclamation_mark: Warning: this is a pre-released hobby project, currently under development

  ProtoCom1  -=Q&A=-

  ## What ProtoCom1 does?
      * One simple serial protocol addresses different embedded devices but
        also instructs the master computer to perform additional tasks.
      * High-level protocol encapsulates different custom extensible commands 
        and data types (text, hex, binary) into a simple sequences w/o CRCs.

  ## What are the strong points?
      * The communication library does the binary conversions, the protocol
        remains human-readable and easy to use in consoles and terminals.
      * Compact overhead (generally 2 bytes), it can be implemented in
        low-resource, even cheapest 8-bit microcontrollers with UART. 
  
  ## What is the jazzy thing!
      * The protocol is scriptable, can perform conditional execution, causal: 
        the master can ask for user input and wait for data from the device
        to check parameters and to branch during the script execution.
      * The received data can be stored in script variables or to trigger callbacks 
        for post-processing and graphical visualization with minimum coding effort.

  ## And even more...
      * Different scripts can be designed for tasks like firmware update,
        configuration, diagnostics, tests, data acquisition and saving, etc. 
      * The script can check the device state, version, firmware ID, etc.
        and to addapt the execution flow accordingly.
      * The protocol is AI/LLM friendly and can be integrated with automation
        interfaces, MCPs, external control systems at minimal cost.