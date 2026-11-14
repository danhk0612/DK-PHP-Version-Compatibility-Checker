<?php
class ProbeExplicitClassName
{
    public function name()
    {
        return get_class($this);
    }
}
